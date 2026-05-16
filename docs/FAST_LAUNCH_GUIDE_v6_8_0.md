# GameForge v6.8.2 Fast Launch Guide

## Fastest normal launch

Use:

```text
START_GAMEFORGE_FAST.vbs
```

## If it does not open

Use:

```text
_system/START_VISIBLE_IF_PROBLEM.bat
```

## Repair

Use:

```text
REPAIR_GAMEFORGE.bat
```

## Best long-term solution

Build the real Windows app:

```text
BUILD_FAST_REAL_APP.bat
```

That creates a packaged installer/portable app in the `release` folder. A packaged Electron app should not need to run npm setup during every launch.
