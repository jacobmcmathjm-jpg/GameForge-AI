# Building GameForge AI as a Real Windows App

## Quick build

From the main `GF` folder, run:

```bat
BUILD_REAL_APP_INSTALLER.bat
```

The finished installer/portable build will appear in:

```text
release/
```

## Developer commands

From inside the `app` folder:

```bash
npm install
npm start
npm run dist
npm run dist:portable
npm run dist:nsis
```

## What was added

- `electron-builder` config in `app/package.json`
- Windows installer target
- Portable app target
- GitHub Actions Windows build workflow
- `.gitignore`
- release folder

## Recommended next step

Put the folder into a GitHub repository so future versions can be tracked safely.
