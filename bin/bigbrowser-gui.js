#!/usr/bin/env node
'use strict'

const { spawn } = require('child_process')
const path      = require('path')

let electronPath
try {
  electronPath = require('electron')
} catch {
  console.error('\n  BigBrowser GUI requires Electron.')
  console.error('  Install it with:  npm install\n')
  process.exit(1)
}

const appRoot = path.join(__dirname, '..')
const args    = process.argv.slice(2)

const child = spawn(electronPath, [appRoot, ...args], {
  stdio: 'inherit',
  env: { ...process.env }
})

child.on('close', code => process.exit(code || 0))
child.on('error', err => {
  console.error('BigBrowser GUI failed to start:', err.message)
  process.exit(1)
})
