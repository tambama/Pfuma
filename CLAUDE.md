# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Pfuma**, a comprehensive technical analysis indicator for the cTrader platform (cAlgo framework). The project detects various market patterns including swing points, Fair Value Gaps (FVGs), order blocks, liquidity sweeps, market structure changes, and other price action patterns commonly used in Smart Money Concepts (SMC) trading.

## Key Development Commands

### Build Commands
```bash
# Build the solution
dotnet build Pfuma.sln

# Build specific project
dotnet build Pfuma/Pfuma.csproj

# Build for Release
dotnet build Pfuma.sln -c Release
```

### Development Commands
```bash
# Restore NuGet packages
dotnet restore Pfuma.sln

# Clean build artifacts
dotnet clean Pfuma.sln

# Pack for deployment (if needed)
dotnet pack Pfuma/Pfuma.csproj
```

## Architecture Overview

### Core Components

**Main Entry Point**: `Pfuma/Pfuma.cs` - The main indicator class that inherits from cTrader's `Indicator` base class and orchestrates all pattern detection and visualization.

**Event-Driven Architecture**: The system uses an event aggregator pattern (`Services/EventAggregator.cs`) for decoupled communication between components. All pattern detectors publish events when patterns are detected, and other components can subscribe to these events.

**Configuration System**: Centralized settings management through `Core/Configuration/IndicatorSettings.cs` and related configuration classes that group related settings (patterns, market structure, time, visualization, notifications).

### Pattern Detection Framework

**Base Pattern Detector**: `Detectors/Base/BasePatternDetector.cs` provides a template method pattern for all detectors with:
- Pre-detection validation
- Pattern detection logic (abstract method)
- Post-detection validation  
- Event publishing
- Repository storage

**Concrete Detectors**: Located in `Detectors/` directory:
- `FvgDetector.cs` - Fair Value Gap detection
- `OrderBlockDetector.cs` - Order block pattern detection
- `BreakerBlockDetector.cs` - Breaker block detection
- `CisdDetector.cs` - Change in State of Delivery detection
- `UnicornDetector.cs` - Unicorn pattern detection
- `GauntletDetector.cs` - Gauntlet pattern detection
- `RejectionBlockDetector.cs` - Rejection block detection
- `OrderFlowDetector.cs` - Order flow and liquidity sweep detection

### Data Management

**Repository Pattern**: `Repositories/Base/BaseRepository.cs` provides CRUD operations for pattern storage:
- `SwingPointRepository.cs` - Manages swing point data
- `LevelRepository.cs` - Manages detected levels/patterns

**Models**: Core data structures in `Models/` directory:
- `SwingPoint.cs` - Represents swing highs/lows
- `Level.cs` - Represents price levels and patterns
- `Candle.cs` - Enhanced bar/candle representation
- `Direction.cs`, `LevelType.cs`, `SessionType.cs` - Enumerations

### Visualization System

**Base Visualizer**: `Visualization/Base/BaseVisualizer.cs` handles chart drawing operations.

**Pattern-Specific Visualizers**: Each pattern detector has a corresponding visualizer that draws patterns on the chart with appropriate colors, labels, and styling.

### Services Layer

**Core Services**:
- `SwingPointDetector.cs` - Detects swing highs and lows in price data
- `TimeManager.cs` - Handles session times, macro times, and Fibonacci levels
- `NotificationService.cs` - Handles logging and notifications
- `TelegramService.cs` - Optional Telegram bot integration

**Time Management**: `Services/Time/` subdirectory contains specialized time-related services:
- `DailyLevelManager.cs` - Manages daily high/low levels
- `FibonacciManager.cs` - Handles Fibonacci retracement calculations

### Event System

**Event Types**: `Core/Events/` contains all event definitions:
- Pattern detection events (FVG, Order Block, etc.)
- Swing point events (detection, removal, liquidity sweeps)
- Time-based events (macro time entries)

## Development Guidelines

### Adding New Pattern Detectors

1. Create a new detector class inheriting from `BasePatternDetector<T>`
2. Implement the abstract methods: `PerformDetection()`, `PublishDetectionEvent()`, `GetByDirection()`, `IsValid()`
3. Add corresponding visualizer inheriting from `BaseVisualizer<T>`
4. Define any new event types in `Core/Events/`
5. Update the main `Pfuma.cs` class to instantiate and initialize the new detector
6. Add configuration parameters if needed

### Working with the Event System

- Use `IEventAggregator.Publish<T>()` to send events
- Use `IEventAggregator.Subscribe<T>()` to listen for events
- All events should inherit from `PatternEventBase` or be strongly typed
- Events are published asynchronously and handlers should be exception-safe

### Configuration Management

- All user-configurable parameters should be defined as `[Parameter]` attributes in `Pfuma.cs`
- Group related parameters using the `Group` parameter attribute
- Update corresponding settings classes in `Core/Configuration/`
- Settings are passed to components through dependency injection in constructors

### Threading and Performance

- The indicator runs on cTrader's main thread
- All operations must be synchronous
- Use repositories for data caching to avoid repeated calculations
- Pattern validation should be lightweight as it runs on every bar

## Important Implementation Notes

- **cTrader Integration**: This indicator is built for the cTrader platform using the cAlgo API
- **Bar Processing**: Main processing happens in `Calculate(int index)` method on each price bar
- **Swing Point Detection**: Core to most pattern detection - must be enabled for order blocks, order flow, etc.
- **Time Management**: UTC offset handling is crucial for session-based analysis
- **Memory Management**: Repository data should be cleaned up appropriately to prevent memory leaks
- **Visual Elements**: All chart drawings are managed through visualizer classes with proper cleanup in `OnDestroy()`

## Package Dependencies

- `cTrader.Automate` - Core cTrader/cAlgo framework
- `Telegram.Bot` v19.0.0 - Optional Telegram notifications
- .NET 6.0 target framework