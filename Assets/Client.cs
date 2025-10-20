using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.193";
    public int port = 26950;
    public int myId = 0;
    public TCP tcp;

    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        tcp = new TCP();
        //ConnectToServer();
    }

    public void ConnectToServer()
    {
        InitializeClientData();
        tcp.Connect();
    }
    private void OnDestroy()
    {
        tcp.socket.Close();
    }

    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>()
        {

            { (int)ServerPackets.welcome, ClientHandle.Welcome },
        };
        Debug.Log("Initialized packets.");
    }



    public class TCP 
    {
        public TcpClient socket;

        private NetworkStream stream;

        private Packet receivedData;

        private byte[] receiveBuffer;
        public void Connect()
        {
            socket = new TcpClient { ReceiveBufferSize = dataBufferSize, SendBufferSize = dataBufferSize };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult _result)
        {

            // Sau khi kết nối thành công với server thì server đã có 1 socket đại diện cho client và chứa ip + port,....
            // Dựa vào thông tin đó thì server và client đã có thể giao tiếp và biết với nhau thông qua các địa chỉ.
            // Chính vì thế mà socket client và server khác nhau nhưng lại có thể giao tiếp và nhận dữ liệu cho nhau thông qua stream được tạo ra bởi socket.
            // Vì socket tại client và server có thể nói là giống nhau và chung 1 kết nối thế nên sẽ truyền nhận dữ liêu được với nhau.
            // Và để phân biệt các client với nhau thì đã có các ip và port, nếu tạo nhiều client trên 1 máy thì sẽ trùng ip nhưng sẽ khác port, do đó server vẫn biết dữ liệu sẽ đổ về đâu dựa vào ip và port khác nhau giữa các client.
            socket.EndConnect(_result);
            if (!socket.Connected)
            {
                Debug.Log("Disconnect");
                return;
            }
            stream = socket.GetStream();

            receivedData = new Packet();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize ,ReceiveCallback, null);
        }
        /// <summary>
        /// Callback để liên tục nhận các gói tin từ server
        /// </summary>
        /// <param name="_result"></param>
        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    // TODO: disconnect
                    Debug.Log("Disconnect");
                    return;
                }
                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data));

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception e) 
            {
                Debug.Log("Disconnect" + e.ToString());
                // TODO: disconnect
            }
        }

        /// <summary>Handles the data received from the server.
        /// TCP sẽ chia các gói tin thành nhiều lần gửi chứ không nhất thiết là 1 lần, vậy nên dữ liệu sẽ được gửi phân đoạn,và liên tục.
        /// Hàm này xử lý việc kiểm tra xem dữ liệu vừa nhận đã đủ data để lấy ra chưa, nếu chưa thì lại đợi tiếp đến khi lần gửi data tiếp theo có đầy đủ dữ liệu thì mới đọc.
        /// </summary>
        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;
            receivedData.SetBytes(_data);

            // Kiểm tra nếu data còn lại chưa đọc có độ dài nhỏ hơn 4 thì không thể đọc được.
            // 4byte đầu tiên đang dùng để lưu giá trị độ dài của data rồi mà bé hơn 4 có nghĩa là data được nhận chưa đủ.
            if (receivedData.UnreadLength() >= 4)
            {
                _packetLength = receivedData.ReadInt(); // Kiểm tra nếu độ dài của data đã nhận lớn hơn 4 thì đọc được độ dài của gói tin.
                if (_packetLength <=0)
                {
                    return true; // Nếu độ dài gói tin bé hơn hoặc = 0 thì cho phép reset lại data để chờ đợt nhận dữ liệu mới.
                }
            }
            while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength()) // Độ dài gói tin phải >0 và kiểm tra xem phần chưa đọc có đủ dữ liệu để đọc chưa, nếu chưa thì bỏ qua và đợi cho đợt dữ liệu mới trả về đầy đủ.
            {
                byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() => //đưa vào dictionary để gọi data.
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt(); // Đọc ID của packet (phía bên server đang là (int)ServerPackets.welcome ) và lưu trữ nó làm key.
                        packetHandlers[_packetId](_packet);// Gọi hàm để sử dụng đống data.
                    }
                });
                _packetLength = 0;
                if (receivedData.UnreadLength() >= 4) // sau khi đọc xong dữ liệu mà vẫn còn dư, thì có nghĩa là dữ liệu còn tiếp, tiếp tục đọc.
                {
                    _packetLength = receivedData.ReadInt(); // tiếp tục đọc độ dài phần gói tin tiếp theo.
                    if (_packetLength <= 0) // nếu độ dài phần gói tin tiếp theo mà <=0 thì reset lai data.
                    {
                        return true;
                    }
                }
            }

            if(_packetLength <= 1)
            {
                return true;
            }

            return false;
        }

    }


}

