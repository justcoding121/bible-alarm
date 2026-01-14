# Lookup Path Comparison: Harvester vs Current Implementation

## Summary of Changes Made

### 1. Bible Publications (bi12, nwt)
**Harvester Format:**
```
?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={languageCode}
```

**Previous Lookup Format:**
```
?output=json&pub={publicationCode}&fileformat=MP3&langwritten={languageCode}&txtCMSLang=E&sectionnum={sectionNumber}&track={trackNumber}
```

**Fixed Lookup Format:**
```
?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&langwritten={languageCode}&txtCMSLang={languageCode}&track={trackNumber}
```

**Changes:**
- ✅ Changed `sectionnum` to `booknum` (matches harvester)
- ✅ Changed `txtCMSLang=E` to `txtCMSLang={languageCode}` (matches harvester)
- ✅ Kept `track` parameter for individual track lookup (harvester doesn't use it when fetching all tracks)
- ⚠️ Removed `alllangs=0` (not needed for individual track lookup)

**Track Matching:**
- Updated to match tracks by `track` property in response (harvester extracts track from `track` property)

---

### 2. Video Publications (gnj)
**Harvester Format:**
```
?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={episodeNumber}
```

**Previous Lookup Format:**
```
?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&txtCMSLang=E&track={trackNumber}
```

**Fixed Lookup Format:**
```
?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={trackNumber}
```

**Changes:**
- ✅ Removed `txtCMSLang` parameter (harvester doesn't use it for videos)
- ✅ Kept `track` parameter (matches harvester)

---

### 3. Drama Publications (Dramas, DramaticBibleReadings)
**Harvester Format:**
```
GET {JwOrgMediatorApiBaseUrl}/categories/{languageCode}/{categoryKey}?detailed=1
```

**Current Lookup Format:**
```
GET {JwOrgMediatorApiBaseUrl}/categories/{languageCode}/{categoryKey}?detailed=1
```

**Status:** ✅ **Already Correct**
- Uses Mediator API (matches harvester)
- Extracts tracks from `category.media` array (matches harvester)
- Matches by `naturalKey` or `track` property (matches harvester)

---

### 4. Music Publications

#### Melody Music (iam)
**Harvester Format:**
```
?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0&langwritten=E&txtCMSLang=E
```
Where `publicationDownloadCode` = "iam-1", "iam-2", "iam-3", etc.

**Previous Lookup Format:**
```
?output=json&pub={pubCode}&fileformat=MP3&langwritten=E&txtCMSLang=E&track={trackNum}
```

**Fixed Lookup Format:**
```
?output=json&pub={downloadCode}&fileformat=MP3&langwritten=E&txtCMSLang=E&track={originalTrackNumber}
```

**Changes:**
- ✅ Uses `DownloadCode` (e.g., "iam-1", "iam-2") stored in database (matches harvester)
- ✅ Uses `OriginalTrackNumber` (track number within disc) stored in database (matches harvester)
- ✅ Changed `txtCMSLang=E` to `txtCMSLang={cmsLang}` where `cmsLang = languageCode ?? "E"` (matches harvester)
- ⚠️ Removed `alllangs=0` (not needed for individual track lookup)

#### Vocal Music (osg, pksjj, sjjc, sjji, snv)
**Harvester Format:**
```
?output=json&pub={publicationCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={cmsLang}
```

**Previous Lookup Format:**
```
?output=json&pub={pubCode}&fileformat=MP3&langwritten={languageCode}&txtCMSLang=E&track={trackNum}
```

**Fixed Lookup Format:**
```
?output=json&pub={publicationCode}&fileformat=MP3&langwritten={languageCode}&txtCMSLang={cmsLang}&track={trackNumber}
```

**Changes:**
- ✅ Changed `txtCMSLang=E` to `txtCMSLang={cmsLang}` where `cmsLang = languageCode ?? "E"` (matches harvester)
- ⚠️ Removed `alllangs=0` (not needed for individual track lookup)

---

## Key Differences Between Harvester and Lookup

1. **Harvester fetches ALL tracks** for a section/publication, then extracts individual tracks from the response
2. **Lookup fetches a SPECIFIC track** using the `track` parameter (if supported by API)
3. **Track matching**: Even with `track` parameter, we match by `track` property in response to ensure accuracy

## Testing Checklist

- [ ] Bible publications (bi12, nwt) - sectioned publications
- [ ] Video publications (gnj) - non-sectioned publications
- [ ] Drama publications (Dramas, DramaticBibleReadings) - Mediator API
- [ ] Melody music (iam) - with disc codes
- [ ] Vocal music (osg, pksjj, sjjc, sjji, snv) - language-specific
