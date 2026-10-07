using Virtuademy.SDK.Environments;
using Virtuademy.Environments.ScriptingApi.ObjectSpawner;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy spawnable: Spawn Spawnable Object Node")]
    [UnitSurtitle("Spawnable")]
    [UnitShortTitle("SpawnSpawnableObjectNode")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SpawnSpawnableObjectNode : AwaitableUnit
    {
        [NullMeansSelf]
        public ValueInput SpawnableObject { get; private set; }

        public ValueInput SpawnPosition { get; private set; }


        protected override void Definition()
        {

            SpawnableObject = ValueInput<SpawnableObjectPlaceholder>(nameof(SpawnableObject), null).NullMeansSelf();
            SpawnPosition = ValueInput<Transform>(nameof(SpawnPosition), null).NullMeansSelf();

            base.Definition();

        }

        /*private ControlOutput Output(Flow flow)
        {
            return outputTrigger;
        }*/

        protected override async Task AwaitableAction(Flow flow)
        {
            SpawnableObjectPlaceholder spawnablePlaceholder = flow.GetValue<SpawnableObjectPlaceholder>(SpawnableObject);
            Transform spawnPosition = flow.GetValue<Transform>(SpawnPosition);
            Vector3 spawnPos = spawnPosition.position;
            Quaternion spawnRot = spawnPosition.rotation;

            object[] data = new object[]{
                new Dictionary<string, object>
                {
                    {
                        "GeneralContainerSpawn", new Dictionary<string, object>
                        {
                            { "spawnIndex", spawnablePlaceholder.indexSpawnReference },
                            { "listToUse", spawnablePlaceholder.listToUse }
                        }
                    }

                }
            };

            // The instance was already discarded before this went through the grouped surface.
            IVirtuademyGameplay.Current.Scene.SpawnContainer(
                spawnPos, spawnRot, IVirtuademyFramework.Current.Session.IsMultiplayer, data);
        }
    }
}
