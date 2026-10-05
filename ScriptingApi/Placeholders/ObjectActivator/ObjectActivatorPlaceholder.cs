using Virtuademy.Environments.ScriptingApi.Placeholders;

using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class ObjectActivatorPlaceholder : SceneComponentPlaceholderBase
    {
        [SerializeField] private List<GameObject> vrComponents = new();
        [SerializeField] private List<GameObject> desktopComponents = new();

        public List<GameObject> VRComponents => vrComponents;
        public List<GameObject> DesktopComponents => desktopComponents;
    }

}

