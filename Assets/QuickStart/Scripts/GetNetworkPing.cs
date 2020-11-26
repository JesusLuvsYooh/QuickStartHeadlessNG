using Mirror;

namespace Assets.QuickStart.Scripts
{
    public class GetNetworkPing : NetworkBehaviour
    {
        private void Awake()
        {
            NetIdentity.OnStartLocalPlayer.AddListener(Setup);
        }

        private void Setup()
        {
            var display = FindObjectOfType<NetworkPingDisplay>(true);
            display.Client = NetIdentity.Client;
            display.gameObject.SetActive(true);
        }
    }
}
