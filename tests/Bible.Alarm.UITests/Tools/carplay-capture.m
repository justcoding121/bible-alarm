#import <Foundation/Foundation.h>
#import <ImageIO/ImageIO.h>
#import <CoreGraphics/CoreGraphics.h>
#import <CoreFoundation/CoreFoundation.h>
#include <mach/mach.h>

typedef struct __IOSurface *IOSurfaceRef;
extern const CFStringRef kIOSurfaceWidth;
extern const CFStringRef kIOSurfaceHeight;
extern const CFStringRef kIOSurfaceBytesPerElement;
extern const CFStringRef kIOSurfaceBytesPerRow;
extern const CFStringRef kIOSurfacePixelFormat;
extern IOSurfaceRef IOSurfaceCreate(CFDictionaryRef properties);
extern void IOSurfaceLock(IOSurfaceRef buffer, uint32_t options, uint32_t *seed);
extern void IOSurfaceUnlock(IOSurfaceRef buffer, uint32_t options, uint32_t *seed);
extern void *IOSurfaceGetBaseAddress(IOSurfaceRef buffer);
extern size_t IOSurfaceGetWidth(IOSurfaceRef buffer);
extern size_t IOSurfaceGetHeight(IOSurfaceRef buffer);
extern size_t IOSurfaceGetBytesPerRow(IOSurfaceRef buffer);
extern uint32_t IOSurfaceGetSeed(IOSurfaceRef buffer);

extern mach_port_t CARenderServerGetServerPort(void);
extern void CARenderServerRenderDisplay(mach_port_t port, CFStringRef name, IOSurfaceRef surface, int x, int y);

static const uint32_t kReadOnly = 1;

static size_t RgbSamples(IOSurfaceRef surface) {
    IOSurfaceLock(surface, kReadOnly, NULL);
    size_t width = IOSurfaceGetWidth(surface);
    size_t height = IOSurfaceGetHeight(surface);
    size_t rowBytes = IOSurfaceGetBytesPerRow(surface);
    uint8_t *base = IOSurfaceGetBaseAddress(surface);
    size_t colored = 0;
    for (size_t y = 0; y < height; y += 2) {
        uint8_t *row = base + y * rowBytes;
        for (size_t x = 0; x < width; x += 2) {
            uint8_t *pixel = row + x * 4;
            if (pixel[0] || pixel[1] || pixel[2]) colored++;
        }
    }
    IOSurfaceUnlock(surface, kReadOnly, NULL);
    return colored;
}

static bool SavePng(IOSurfaceRef surface, const char *path) {
    IOSurfaceLock(surface, kReadOnly, NULL);
    size_t width = IOSurfaceGetWidth(surface);
    size_t height = IOSurfaceGetHeight(surface);
    size_t rowBytes = IOSurfaceGetBytesPerRow(surface);
    void *base = IOSurfaceGetBaseAddress(surface);
    CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
    CGDataProviderRef provider = CGDataProviderCreateWithData(NULL, base, rowBytes * height, NULL);
    CGImageRef image = CGImageCreate(width, height, 8, 32, rowBytes, colorSpace,
        kCGBitmapByteOrder32Little | kCGImageAlphaPremultipliedFirst, provider, NULL, false, kCGRenderingIntentDefault);
    CFURLRef url = CFURLCreateFromFileSystemRepresentation(NULL, (const UInt8 *)path, strlen(path), false);
    CGImageDestinationRef dest = CGImageDestinationCreateWithURL(url, CFSTR("public.png"), 1, NULL);
    bool ok = false;
    if (dest) {
        CGImageDestinationAddImage(dest, image, NULL);
        ok = CGImageDestinationFinalize(dest);
        CFRelease(dest);
    }
    CFRelease(url);
    CGImageRelease(image);
    CGDataProviderRelease(provider);
    CGColorSpaceRelease(colorSpace);
    IOSurfaceUnlock(surface, kReadOnly, NULL);
    return ok;
}

int main(int argc, char **argv) {
    @autoreleasepool {
        setvbuf(stdout, 0, _IONBF, 0);
        const char *path = argc > 1 ? argv[1] : "/tmp/bible-alarm-carplay.png";
        NSDictionary *props = @{
            (__bridge id)kIOSurfaceWidth: @800,
            (__bridge id)kIOSurfaceHeight: @480,
            (__bridge id)kIOSurfaceBytesPerElement: @4,
            (__bridge id)kIOSurfaceBytesPerRow: @3200,
            (__bridge id)kIOSurfacePixelFormat: @(0x42475241)
        };
        IOSurfaceRef surface = IOSurfaceCreate((__bridge CFDictionaryRef)props);
        mach_port_t port = CARenderServerGetServerPort();
        CARenderServerRenderDisplay(port, CFSTR("TVOut"), surface, 0, 0);
        size_t colored = RgbSamples(surface);
        bool saved = SavePng(surface, path);
        printf("tvout colored=%zu saved=%d path=%s\n", colored, saved, path);
        CFRelease(surface);
        return (saved && colored > 1000) ? 0 : 1;
    }
}