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