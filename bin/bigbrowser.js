#!/usr/bin/env node
'use strict'

const BigBrowserCLI = require('../src/cli/browser')

const args       = process.argv.slice(2)
const initialUrl = args.find(a => !a.startsWith('-')) || null

const browser = new BigBrowserCLI()
browser.run(initialUrl)
