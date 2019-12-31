using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// State object for reading client data asynchronously  
namespace SonataDiscordProxyBot
{
	public class SocketServer
	{
		private Program program;
		private TcpListener tcpListener;
		private Thread tcpListenerThread;
		private TcpClient connectedTcpClient;

		public void Start(Program program)
		{
			tcpListenerThread = new Thread(new ThreadStart(ListenForIncommingRequests));
			tcpListenerThread.IsBackground = true;
			tcpListenerThread.Start();
			this.program = program;
		}

		private void ListenForIncommingRequests()
		{
			try
			{		
				tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 11000);
				tcpListener.Start();
				Byte[] bytes = new Byte[1024];
				while (true)
				{
					using (connectedTcpClient = tcpListener.AcceptTcpClient())
					{				
						using (NetworkStream stream = connectedTcpClient.GetStream())
						{
							int length;						
							while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
							{
								var incommingData = new byte[length];
								Array.Copy(bytes, 0, incommingData, 0, length);						
								string clientMessage = Encoding.ASCII.GetString(incommingData);
								HandleRequest(clientMessage);
							}
						}
					}
				}
			}
			catch (SocketException socketException)
			{
				Console.WriteLine("SocketException " + socketException.ToString());
			}
		}

		private void HandleRequest(string request)
		{
			switch (request)
			{
				case "online":
					{
						if(program.onlineCharacters.Count == 0)
						{
							SendMessage("No players are online");
						}
						else
						{
							SendMessage(string.Join(", ", program.onlineCharacters));
						}
					} break;

				default: SendMessage("Invalid request"); break;
			}
		}

		private void SendMessage(string message)
		{
			if (connectedTcpClient == null)
			{
				return;
			}

			try
			{			
				NetworkStream stream = connectedTcpClient.GetStream();
				if (stream.CanWrite)
				{
					byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(message);
					stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
				}
			}
			catch (SocketException socketException)
			{
				Console.WriteLine("Socket exception: " + socketException);
			}
		}
	}
}