# Unix Browser Configuration Documentation

## Performance Optimization

### Hardware Acceleration
- Enabled by default for fast rendering
- Uses GPU for WebGL rendering
- Provides smooth scrolling and animations

### Caching Strategy
- 100MB disk cache for resources
- Optimized for fast page loads
- Service Workers extend caching capabilities

### Memory Management
- Efficient resource cleanup
- Configurable cache limits
- JIT compilation enabled for JavaScript

## Browser Capabilities

### Supported Standards
- HTML5 / CSS3 / ES6+
- WebSockets
- WebGL
- Service Workers
- Offline Storage (IndexedDB, LocalStorage)
- Web Notifications

### PWA Features
- Installation to Start Menu
- Standalone mode
- Offline functionality
- Background sync
- Push notifications (via Service Workers)

### React Native Support
- Detects React Native web apps
- Provides platform information
- Enables device capabilities access
- Compatible with Expo web

## Security Features

- Sandboxed JavaScript execution
- HTTPS enforcement for PWAs
- Content Security Policy support
- Cross-origin protection

## Customization

### Theme Colors
Edit `Config/BrowserConfig.cs`:

```csharp
public const string ThemeBackground = "#1a1a1a";    // Main background
public const string ThemeForeground = "#e0e0e0";    // Text color
public const string ThemeAccent = "#00ff00";        // Highlight color
public const string ThemeBorder = "#333333";        // Border color
```

### User Agent
Modify the user agent string to identify the browser:

```csharp
public static string GetUserAgent() => 
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) UnixBrowser/1.0 (PWA-Capable; ReactNative-Compatible)";
```

## Development

### Enable DevTools
Set `AreDevToolsEnabled = true` in `BrowserEngine.ConfigureSettings()` for debugging.

### Logging
Check the Debug output window in Visual Studio for:
- Navigation events
- Script execution errors
- PWA and Service Worker status

## Performance Tips

1. **Clear Cache Regularly**: Large caches can slow down the browser
2. **Update WebView2**: Keep the WebView2 runtime updated
3. **Disable Unnecessary Extensions**: Disable DevTools in production builds
4. **Monitor Memory**: Watch memory usage in Task Manager

## Known Limitations

- Single process per window
- No extension support
- Limited video codec support (system-dependent)
- JavaScript execution is Chromium-based

## Future Enhancements

- [ ] Tab management system
- [ ] Download manager
- [ ] History/Bookmarks
- [ ] Search bar integration
- [ ] Profile management
- [ ] Session persistence
- [ ] Network throttling (DevTools)
