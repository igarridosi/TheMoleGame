@echo off
echo Proiektua konpilatzen...
dotnet build

echo.
echo Zerbitzaria abiarazten...
start "SERVER" "Server\bin\Debug\net8.0\Server.exe"

echo.
echo Zerbitzaria kargatzen itxaroten (2 segundu)...
timeout /t 2 /nobreak >nul

echo.
echo 6 Bezero abiarazten...
start "Client 1" "Client\bin\Debug\net8.0-windows\Client.exe"
start "Client 2" "Client\bin\Debug\net8.0-windows\Client.exe"
start "Client 3" "Client\bin\Debug\net8.0-windows\Client.exe"
start "Client 4" "Client\bin\Debug\net8.0-windows\Client.exe"
start "Client 5" "Client\bin\Debug\net8.0-windows\Client.exe"
start "Client 6" "Client\bin\Debug\net8.0-windows\Client.exe"

echo.
echo Eginda!
exit