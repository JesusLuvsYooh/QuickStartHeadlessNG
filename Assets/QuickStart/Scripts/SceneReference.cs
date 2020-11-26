using UnityEngine;

namespace QuickStart
{
    public class SceneReference : MonoBehaviour
    {
        //workaround for network idendity objects not always being active at scene change (awake)
        //objects such as player .Find this, and now player has access to the networked sceneScript, even if it was disabled
        public SceneScript sceneScript;
        public GameObject netPing;
    }
}