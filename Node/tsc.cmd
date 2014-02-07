@echo off
%~dp0/node "%~dp0/node_modules/typescript/bin/tsc" %*
echo Done compiling.