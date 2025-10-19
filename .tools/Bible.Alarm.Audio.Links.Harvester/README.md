# Bible Alarm Audio Links Harvester

A modularized console application for harvesting audio links from JW.org for Bible and Music content.

## Architecture

The application has been refactored to use dependency injection and follows a clean architecture pattern with clear separation of concerns.

### Project Structure

```
.tools/Bible.Alarm.Audio.Links.Harvester/
├── Services/
│   ├── Contracts/           # Service interfaces
│   │   ├── IBibleHarvesterService.cs
│   │   ├── IMusicHarvesterService.cs
│   │   ├── IIndexService.cs
│   │   ├── ICloudPublishingService.cs
│   │   ├── IFileSystemService.cs
│   │   ├── IConfigurationService.cs
│   │   └── IHarvestingOrchestratorService.cs
│   └── Infrastructure/     # Service implementations
│       ├── BibleHarvesterService.cs
│       ├── MusicHarvesterService.cs
│       ├── IndexService.cs
│       ├── CloudPublishingService.cs
│       ├── FileSystemService.cs
│       ├── ConfigurationService.cs
│       ├── HarvestingOrchestratorService.cs
│       └── ServiceCollectionExtensions.cs
├── Harvesters/             # Legacy harvesters (to be removed)
│   ├── Bible/
│   └── Music/
└── Program.cs              # Main entry point
```

## Services

### Core Services

- **IHarvestingOrchestratorService**: Main orchestrator that coordinates the entire harvesting process
- **IConfigurationService**: Manages application configuration from environment variables
- **IFileSystemService**: Handles file system operations (cleanup, validation, directory management)

### Harvester Services

- **IBibleHarvesterService**: Harvests Bible audio links from JW.org
- **IMusicHarvesterService**: Harvests Music (Vocal and Melody) audio links from JW.org

### Processing Services

- **IIndexService**: Creates and manages index files and metadata
- **ICloudPublishingService**: Handles publishing to AWS S3/CloudFront

## Key Features

### Dependency Injection
- Uses Microsoft.Extensions.DependencyInjection for simple IoC container
- All services are registered as singletons for optimal performance
- Easy to test and mock individual services
- Simple ServiceCollection approach (no HostBuilder overhead)

### Modular Design
- Each service has a single responsibility
- Clear interfaces make the code testable and maintainable
- Services can be easily replaced or extended

### Error Handling
- Comprehensive error handling in the orchestrator
- Detailed logging and error reporting
- Graceful failure handling

### Configuration Management
- Environment variable-based configuration
- Type-safe configuration access
- Easy to configure for different environments

## Usage

```bash
dotnet run
```

The application will:
1. Clean up the index directory
2. Harvest Bible links in parallel with Music links
3. Create index files and metadata
4. Zip the results
5. Publish to S3/CloudFront

## Configuration

The application uses environment variables for configuration:

- AWS credentials (configured via AWS SDK defaults)
- Directory paths (configured in shared utilities)
- Publication mappings (hardcoded in services)

## Benefits of Modularization

1. **Testability**: Each service can be unit tested independently
2. **Maintainability**: Clear separation of concerns makes code easier to maintain
3. **Extensibility**: New harvesters or services can be easily added
4. **Reusability**: Services can be reused in other applications
5. **Configuration**: Easy to configure for different environments
6. **Error Handling**: Better error isolation and handling
7. **Performance**: Optimized service registration and lifecycle management

## Migration from Legacy Code

The original monolithic `Program.cs` has been broken down into:

- **BibleHarvesterService**: Contains the logic from `JwBibleHarvester`
- **MusicHarvesterService**: Contains the logic from `MusicHarvester`
- **IndexService**: Contains the logic for creating index files
- **CloudPublishingService**: Contains the S3 publishing logic
- **FileSystemService**: Contains file system operations
- **HarvestingOrchestratorService**: Orchestrates the entire process

The legacy harvesters in the `Harvesters/` folder can now be removed as their functionality has been moved to the appropriate services.
