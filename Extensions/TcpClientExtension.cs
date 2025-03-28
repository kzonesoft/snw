using System;
using System.Net.Sockets;

namespace Kzone.Signal.Extensions
{
    public static class TcpClientExtensions
    {
#if NET40
       
        public static void Dispose(this TcpClient tcpClient)
        {
            if (tcpClient != null)
            {
                try
                {
                    if (tcpClient.Client != null && tcpClient.Client.Connected)
                    {
                        // Shutdown the socket to stop further communication
                        tcpClient.Client.Shutdown(SocketShutdown.Both);
                    }
                }
                catch (SocketException ex)
                {
                    // Log SocketException (optional)
                    Console.WriteLine($"SocketException during TcpClient disposal: {ex.Message}");
                }
                catch (ObjectDisposedException)
                {
                    // Ignore as the socket might already be disposed
                }
                catch (Exception ex)
                {
                    // Log any unexpected exceptions (optional)
                    Console.WriteLine($"Unexpected error during TcpClient disposal: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        // Close the underlying socket
                        tcpClient.Client?.Close();
                    }
                    catch (Exception ex)
                    {
                        // Log any errors during socket close (optional)
                        Console.WriteLine($"Error while closing socket: {ex.Message}");
                    }
                }
            }
        }
#endif
    }

}
