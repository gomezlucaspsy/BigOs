# WasmShare — P2P File Transfer via QR + WebAssembly

Transfiere archivos directamente entre dos navegadores sin servidor intermediario.
Usa **WebRTC** para la conexión P2P, **WebAssembly** para XOR encoding de los chunks
y un **QR** para compartir la URL de emparejamiento.

---

## Arquitectura

```
Sender browser                    Receiver browser
      │                                  │
      │──── WebSocket signaling ─────────│  (solo para el handshake)
      │                                  │
      │══════════ RTCDataChannel ════════│  (datos P2P directos)
      │   chunks XOR'd via Wasm          │   chunks decoded via Wasm
```

---

## Requisitos

| Herramienta | Para qué |
|-------------|----------|
| Node.js ≥ 18 | Servidor de señalización |
| [wabt](https://github.com/WebAssembly/wabt) (`wat2wasm`) | Compilar el módulo `.wat` → `.wasm` |

---

## Instalación y arranque

```bash
cd wasm-share

# 1. Instalar dependencias Node
npm install

# 2. Compilar el módulo Wasm (necesita wabt en el PATH)
npm run build:wasm

# 3. Arrancar el servidor
npm start
# → http://localhost:3000
```

---

## Uso

1. Abre `http://localhost:3000` en el **emisor**.
2. Selecciona un archivo y pulsa **"Generar QR y compartir"**.
3. Escanea el QR con el **receptor** (otro dispositivo o pestaña).
4. La transferencia comienza automáticamente — el receptor verá el botón de descarga al terminar.

---

## Módulo WebAssembly (`compress.wat`)

El módulo exporta:

```wat
xor_buffer(offset i32, length i32, key i32) → void
```

Aplica XOR byte a byte sobre la memoria lineal compartida. Al ser simétrico,
el mismo módulo y la misma clave sirven tanto para codificar (emisor) como
para decodificar (receptor).

> **Nota:** XOR es un cifrado demostrativo. Para producción usa
> [libsodium.js](https://github.com/jedisct1/libsodium.js) o compila
> una librería de cifrado real a Wasm con Emscripten/wasm-pack.

---

## Estructura de archivos

```
wasm-share/
├── server.js              # Servidor HTTP + WebSocket (señalización)
├── package.json
└── public/
    ├── index.html         # UI
    ├── main.js            # Lógica WebRTC + Wasm (ES module)
    └── wasm/
        ├── compress.wat   # Fuente Wasm (texto)
        └── compress.wasm  # Binario compilado (generado por npm run build:wasm)
```
