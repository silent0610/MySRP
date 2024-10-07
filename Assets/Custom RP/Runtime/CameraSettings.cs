using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings {

	[Serializable]
	public struct FinalBlendMode {

		public BlendMode source, destination;
	}
	public bool copyDepth = true;
	public bool copyColor = true;

	[RenderingLayerMaskField]
    public int renderingLayerMask = -1;
	public bool maskLights = false;
    public bool overridePostFX = false;

    
    public PostFXSettings postFXSettings = default;

    

    public FinalBlendMode finalBlendMode = new FinalBlendMode {
		source = BlendMode.One,
		destination = BlendMode.Zero
	};

	public enum RenderScaleMode { Inherit, Multiply, Override }

	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

	[Range(0.1f, 2f)]
	public float renderScale = 1f;
	public float GetRenderScale(float scale) {
		return
			renderScaleMode == RenderScaleMode.Inherit ? scale :
			renderScaleMode == RenderScaleMode.Override ? renderScale :
			scale * renderScale;
	}

}
