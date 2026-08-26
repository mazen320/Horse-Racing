using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using UTool.TabSystem;

namespace UTool.Networking
{
    public class UNetCommLink : MonoBehaviour
    {
        [SerializeField] private TabName tabName = TabName.UNetComm;
        [SerializeField][Disable] private UNetComm uNetComm;

        [SpaceArea, Line(5)]

        [SerializeField][BeginGroup] public UnityEvent<string, bool> OnClientConnection = new UnityEvent<string, bool>();
        [SerializeField] public UnityEvent<string, bool> OnConnectionToServer = new UnityEvent<string, bool>();
        [SerializeField][EndGroup] public UnityEvent<string, byte[]> OnDataReceived = new UnityEvent<string, byte[]>();

        private void Start()
        {
            uNetComm = UNetComm.GetInstance(tabName);

            if (uNetComm)
                uNetComm.AddLink(this);
        }

        private void OnDestroy()
        {
            if (uNetComm)
                uNetComm.RemoveLink(this);
        }

        public void SendData(byte[] data)
        {
            if (uNetComm)
                uNetComm.Send(data);
        }

        public void SendData(string ipPort, byte[] data)
        {
            if (uNetComm)
                uNetComm.SendToTCPClient(ipPort, data);
        }

        public void Broadcast(byte[] data)
        {
            if (uNetComm)
                uNetComm.Broadcast(data);
        }
    }
}