using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    [AddComponentMenu("")]//For hide from add component menu.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Variables))]
    public class SyncedVariables : MonoBehaviour
    {
        public override string ToString()
        {
            //Data.ToString();
            string value = "";
            foreach (Data data in variableSettings)
            {

                value = value + data.ToString() + "\n";

            }
            return value;
        }

        [System.Serializable]
        public class Data
        {
            public override string ToString()
            {
                string value = "name: " + name + "value: " + DeclarationValue + " { hasChanged: " + hasChanged + " + AreSynced: " + AreValuesSynced + " }";
                return value;
            }

            [HideInInspector]
            public byte id;
            public string name;
            public bool saveThroughSessions;
            /// <summary>
            /// Keeps track of wheter or not the value has ever been changed on the network from the initial value
            /// if the variable is not synced it is always false
            /// </summary>
            public bool hasChanged = false;
            /// <summary>
            /// The value of the variable used to check if the variables in VS are changed
            /// it holds the value of the variable before any changes
            /// </summary>
            public object previousValue;

            /// <summary>
            /// The VS related variable
            /// </summary>
            public VariableDeclaration declaration { get; set; }

            public object DeclarationValue
            {
                get
                {
                    return declaration.value;
                }
            }

            public bool AreValuesSynced
            {
                get
                {
                    return Equals(DeclarationValue, previousValue);
                }
            }

            public void SyncValues()
            {
                previousValue = DeclarationValue;
                hasChanged = true;
            }
        }

        public List<Data> variableSettings = new List<Data>();
        //public Dictionary<string, int> variableDictLock = new Dictionary<string, int>();

        public void VariableSet()
        {
            foreach (Data data in variableSettings)
            {
                if (data.declaration == null)
                {
                    var declarations = GetComponentInChildren<Variables>(true).declarations;
                    data.declaration = declarations.GetDeclaration(data.name);
                    data.previousValue = data.DeclarationValue;
                }
            }
        }

    }
}
