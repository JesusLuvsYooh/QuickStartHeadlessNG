using Mirror;

namespace Assets.QuickStart.Scripts
{
    public class GetNetworkPing : NetworkBehaviour
    {
        //public void GameObject displayObj  ;
        
        private void Awake()
        {
            NetIdentity.OnStartLocalPlayer.AddListener(Setup);
        }

        private void Setup()
        {
            //a fix for my 2019 editor version does the below elsewhere
            
            //var display = FindObjectOfType<NetworkPingDisplay>(true);
            //display.Client = NetIdentity.Client;
            //display.gameObject.SetActive(true);
        }
    }
}
