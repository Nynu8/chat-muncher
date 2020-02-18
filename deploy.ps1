cd SonataDiscordProxyBot
dotnet publish -c release --runtime linux-arm --framework netcoreapp3.0
cd 
$Server = Read-Host -Prompt 'Input SSH server address: '
scp bin\release\netcoreapp3.0\linux-arm\publish\* pi@${Server}:/home/pi/server/chat_muncher/
cd ..