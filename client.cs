using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.IO;

class Program
{
    static void Main()
    {
        string serverAddress = "localhost"; // Replace with the actual server address
        int serverPort = 3000; // The port where the server is listening

        TcpClient client = null;

        List<Dictionary<string, object>> packetsList = new List<Dictionary<string, object>>();
        List<int> missingSequences = new List<int>();

        try
        {
            client = new TcpClient(serverAddress, serverPort);

            NetworkStream stream = sendRequest(client, 1, 0);

            // Receive and process data from the server
            byte[] buffer = new byte[17]; // Assuming a maximum packet size of 17 bytes

            int expectedSequence = 1;

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Server has closed the connection
                    break;
                }

                // Process the received packet and add to the list
                var packet = ProcessPacket(buffer);
                while ((int)packet["Packet Sequence"] != expectedSequence) {
                    missingSequences.Add(expectedSequence);
                    packetsList.Add(new Dictionary<string, object>());
                    expectedSequence++;
                }
                packetsList.Add(packet);
                expectedSequence++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally {
            client?.Close();
        }

        foreach (int missingSequence in missingSequences) {
            // Console.WriteLine(missingSequence);
            client = new TcpClient(serverAddress, serverPort);
            NetworkStream stream = sendRequest(client, 2, (byte)missingSequence);
            byte[] buffer = new byte[17];
            stream.Read(buffer, 0, buffer.Length);
            var packet = ProcessPacket(buffer);
            packetsList[missingSequence - 1] = packet;
            client.Close();
        }
        Console.WriteLine();

        // Convert the list to JSON with indentation and print
        var json = Serialize(packetsList);
        using (StreamWriter writer = new StreamWriter("./output.json"))
        {
            writer.Write(json);
        }

        Console.WriteLine(json);
    }

    static NetworkStream sendRequest(TcpClient client, byte callType, byte resendSeq) 
    {
        NetworkStream stream = client.GetStream();
        try 
        {
            // Create a payload buffer
            byte[] payload = new byte[2];
            
            payload[0] = callType;

            if (callType == 2)
            {
                payload[1] = resendSeq;
            }

            // Send the payload to the server
            stream.Write(payload, 0, payload.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        return stream;
    }

    // Method to process a received packet and return a dictionary
    static Dictionary<string, object> ProcessPacket(byte[] packet)
    {
        string symbol = Encoding.ASCII.GetString(packet, 0, 4);
        char buySellIndicator = (char)packet[4];

        // Interpret Quantity and Price as big-endian
        int quantity = BitConverter.ToInt32(new byte[] { packet[8], packet[7], packet[6], packet[5] }, 0);
        int price = BitConverter.ToInt32(new byte[] { packet[12], packet[11], packet[10], packet[9] }, 0);

        // Interpret Packet Sequence as big-endian
        int packetSequence = BitConverter.ToInt32(new byte[] { packet[16], packet[15], packet[14], packet[13] }, 0);

        // Create a dictionary for the packet
        var packetDict = new Dictionary<string, object>
        {
            { "Symbol", symbol },
            { "Buy/Sell Indicator", buySellIndicator.ToString() },
            { "Quantity", quantity },
            { "Price", price },
            { "Packet Sequence", packetSequence }
        };

        return packetDict;
    }

    // Serialize an object to JSON
    static string Serialize(object obj)
    {
        return SerializeObject(obj, 0);
    }

    static string SerializeObject(object obj, int indentation)
    {
        if (obj == null)
        {
            return "null";
        }

        var sb = new StringBuilder();
        bool isFirst = true;

        if (obj is List<Dictionary<string, object>> list)
        {
            sb.Append("[\n");

            foreach (var item in list)
            {
                if (!isFirst)
                {
                    sb.Append(",\n");
                }

                isFirst = false;
                sb.Append(' ', indentation + 2);
                sb.Append("{\n");

                bool isFirstProp = true;
                foreach (var prop in item)
                {
                    if (!isFirstProp)
                    {
                        sb.Append(",\n");
                    }

                    isFirstProp = false;
                    sb.Append(' ', indentation + 4);
                    sb.AppendFormat("\"{0}\": \"{1}\"", prop.Key, prop.Value);
                }

                sb.Append("\n");
                sb.Append(' ', indentation + 2);
                sb.Append("}");
            }

            sb.Append("\n");
            sb.Append(' ', indentation);
            sb.Append("]");
        }

        return sb.ToString();
    }
}
