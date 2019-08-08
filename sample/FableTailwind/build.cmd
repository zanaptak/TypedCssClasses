@echo off
if exist .\deploy\ ( del /q .\deploy\*.* )
call npm run css
call npm run build
