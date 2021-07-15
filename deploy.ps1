cd SonataDiscordProxyBot
dotnet publish -c Release
cd 
$Server = Read-Host -Prompt 'Input SSH server address: '
scp bin\release\net5.0\publish\* pi@${Server}:/home/pi/server/chat-muncher/src
scp Dockerfile pi@${Server}:/home/pi/server/chat-muncher/
scp docker-compose.yml pi@${Server}:/home/pi/server/chat-muncher/
cd ..