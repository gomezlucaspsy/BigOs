#!/usr/bin/env node
'use strict'

const BigOsCLI = require('../src/cli/browser')

const args       = process.argv.slice(2)
const initialUrl = args.find(a => !a.startsWith('-')) || null

const browser = new BigOsCLI()
browser.run(initialUrl)
