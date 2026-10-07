
using System.Collections.Generic;

using UnityEngine;


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public abstract class POIBlockPlaceholder : MonoBehaviour
    {
        [HelpBox("To edit the position/dimension of a block, you can simply modify the values of its RectTransform. " +
            "To edit the content of a block, edit the fields available in the \"Configurable stuff\" " +
            "section of this inspector: the children components preview how the block will be displayed at runtime. " +
            "Text typed directly into a child TextMeshPro is also picked up (the displayed text at runtime is the " +
            "TextMeshPro's, and the inspector field re-aligns to it on selection), but non-text changes to children " +
            "components will be ignored. ",
            HelpBoxMessageType.Info)]

        [Tooltip("Do not edit the addressable keys.")]
        [SerializeField] protected List<string> addressableKeys;

        public virtual string AddressableKey => addressableKeys[0];
    }
}
