# Unix Browser - Installation Guide for PWAs

## How to Install PWA Apps

### Automatic Detection
The Unix Browser automatically detects when a website is a PWA and shows an installation prompt.

### Manual Installation
1. Open a PWA in the browser
2. Look for the install button/prompt
3. Click to install as a standalone app
4. The app will appear in your Windows Start Menu

## Supported PWA Features
- **Offline Support**: Service Workers cache resources
- **Installation**: Add app to Start Menu
- **Standalone Mode**: Run full-screen without browser UI
- **Notifications**: Desktop notification support
- **Shortcuts**: App shortcuts and actions

## React Native Web Apps

### Compatibility
The browser is pre-configured to support React Native web apps including:
- **Personaforge** and similar React Native + Expo web apps
- Custom React Native web builds
- Expo-hosted apps

### Installation from React Native Apps
1. Open the React Native app URL
2. The app automatically detects Unix Browser
3. Install prompt will appear when supported
4. Install like any other PWA

## Example URLs to Test

```
# Personaforge
https://github.com/gomezlucaspsy/personaforge

# Expo Apps
https://expo.dev

# PWA Examples
https://web.dev/explore/progressive-web-apps/
```

## Troubleshooting

### PWA Installation Not Showing
- Ensure the website has a valid manifest.json
- Check that HTTPS is being used
- Try refreshing the page

### React Native App Not Detected
- Check browser console for errors
- Ensure Service Worker registration succeeds
- Verify the app supports web platform

### Service Workers Not Active
- Check DevTools console for registration errors
- Ensure HTTPS is used in production
- Clear browser cache and reload
