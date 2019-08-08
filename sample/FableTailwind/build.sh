#!/bin/sh
if [ -d ./deploy/ ]; then rm ./deploy/*; fi
npm run css
npm run build
