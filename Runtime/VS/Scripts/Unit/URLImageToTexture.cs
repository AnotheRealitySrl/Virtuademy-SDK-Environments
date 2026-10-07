
using System.Threading.Tasks;

using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.UI;


using SPACS.VisualScripting;

using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy image: Set sprite from URL")]
    [UnitSurtitle("Image")]
    [UnitShortTitle("Set sprite from URL")]
    [UnitCategory("Virtuademy\\Flow")]
    public class URLImageToTexture : AwaitableUnit
    {

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput ImageURL { get; private set; }

        [NullMeansSelf]

        public ValueInput ImageValue { get; private set; }

        private Sprite sprite;

        //Convert to coroutine
        protected override void Definition()
        {
            ImageURL = ValueInput<string>(nameof(ImageURL), null);
            ImageValue = ValueInput<Image>(nameof(ImageValue), null).NullMeansSelf();

            base.Definition();
        }

        protected async override Task AwaitableAction(Flow flow)
        {
            Texture2D tex = await ImageDownloader.DownloadImageAsync(flow.GetValue<string>(ImageURL));

            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(tex.width / 2, tex.height / 2));
            flow.GetValue<Image>(ImageValue).sprite = sprite;

        }
    }
}
