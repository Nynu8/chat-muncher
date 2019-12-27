cd SonataDiscordProxyBot
dotnet publish -c release --runtime linux-arm --framework netcoreapp3.0
cd 
$Server = Read-Host -Prompt 'Input SSH server address: '
scp bin\release\netcoreapp3.0\linux-arm\publish\* pi@192.168.0.111:/home/pi/server/chat_muncher/
cd ..