using Colyseus;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{

    private ColyseusClient _client;
    private ColyseusRoom<MyRoomState> _room; // Replace with your schema
    public string roomName;
      void Start()
    {
        InitializeClient();
          
    }

    void InitializeClient()
    {
        _client = new ColyseusClient("ws://localhost:2567");
        // Setup room handlers
        _room.OnStateChange += OnStateChange;
    }
    void OnStateChange(MyRoomState state, bool isFirstState)
    {
        // Handle state changes
    }
    public void conn() {
          ConnectToRoom();
    }
    async System.Threading.Tasks.Task ConnectToRoom()
    {
        try
        {
            _room = await _client.JoinOrCreate<MyRoomState>("ShootingGalleryRoom");
            Debug.Log($"Connected to room {_room.RoomId}");
            var callbacks = Colyseus.Schema.Callbacks.Get(_room);
            // Setup room handlers
            _room.OnMessage<string>("chat", (msg) => {
                Debug.Log($"Chat received: {msg}");
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Connection error: " + ex.Message);
        }
    }
}
