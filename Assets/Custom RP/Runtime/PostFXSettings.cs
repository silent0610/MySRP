using System;
using UnityEngine;
[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject {

	[SerializeField]
	Shader shader = default;

	[NonSerialized]
	Material material;

	public Material Material {
		get {
			if (material == null && shader != null) { 
				material = new Material(shader);
				material.hideFlags = HideFlags.HideAndDontSave;
			}
			return material;
		}
	}

	[Serializable]
	public struct BloomSettings {

		public bool ignoreRenderScale;

		[Range(0f, 16f)]
		public int maxIterations ;
		[Min(1f)]
		public int downscaleLimit ;

		public bool bicubicUpsampling;
		[Min(0f)]
		public float threshold;

		[Range(0f, 1f)]
		public float thresholdKnee;
		
		[Min(0f)]
		public float intensity;//bloom 强度

		public bool fadeFireflies; //闪烁
		public enum Mode { Additive, Scattering }

		public Mode mode;

		[Range(0.05f, 0.95f)]
		public float scatter;
	}

	[SerializeField]
	BloomSettings bloom = new BloomSettings {
		scatter = 0.7f
	};
	public BloomSettings Bloom => bloom;


	//颜色分级
	[Serializable]
	public struct ColorAdjustmentsSettings {
		public float postExposure;//后曝光

		[Range(-100f, 100f)]
		public float contrast;//对比度

		[ColorUsage(false, true)]
		public Color colorFilter;//颜色滤镜

		[Range(-180f, 180f)]
		public float hueShift;//色调偏移

		[Range(-100f, 100f)]
		public float saturation;//饱和度

	}
	[SerializeField]
	ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings {
		colorFilter = Color.white
	};
	public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

	//白平衡
    [Serializable]
    public struct WhiteBalanceSettings
    {

        [Range(-100f, 100f)]
        public float temperature, tint;
    }

    [SerializeField]
    WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

	//色调分割,单独调整阴影和镜面反射颜色
    [Serializable]
    public struct SplitToningSettings
    {
        //这些颜色不会被用于着色计算?
		//似乎是不带alpha
        [ColorUsage(false)]
        public Color shadows, highlights;

        [Range(-100f, 100f)]
        public float balance;
    }

    [SerializeField]
    SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

	//色彩混合
    [Serializable]
    public struct ChannelMixerSettings
    {

        public Vector3 red, green, blue;
    }

    [SerializeField]
    ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    //阴影\中间色调\高光
    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {

        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;

        [Range(0f, 2f)]
        public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
    }

    [SerializeField]
    ShadowsMidtonesHighlightsSettings
        shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1f
        };

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
        shadowsMidtonesHighlights;




    //色调映射
    [Serializable]
	public struct ToneMappingSettings { //Tone Mapping
		//ReinHard  c/1+c
		public enum Mode { None ,ACES, Neutral, Reinhard }

		public Mode mode;
	}

	[SerializeField]
	ToneMappingSettings toneMapping = default;

	public ToneMappingSettings ToneMapping => toneMapping;
}