using System.Runtime.InteropServices;
//即贡献数据在内存中的布局
/*
 * 
 * 隐式转换：
 * 本质：数值转换，将 int 的数值精确地转换为 float，但它会改变底层的位表示。
 * 用途：用于将整数转换为浮点数，并保持数值一致。
 * 例子：int 42 变成 float 42.0f。
 * 
 * 按位重新解释：
 * 本质：位级转换，保持二进制模式不变，但使用不同的解释方式来读取数据。
 * 用途：用于处理低级别的内存数据，像是需要处理特殊二进制表示或解码数据流时。
 * 例子：int 1065353216 会被解释为 float 1.0f，因为这两者的二进制位模式是相同的。
 */
public static class ReinterpretExtensions {
	[StructLayout(LayoutKind.Explicit)]
	struct IntFloat {

		[FieldOffset(0)]
		public int intValue;

		[FieldOffset(0)]
		public float floatValue;
	}
	public static float ReinterpretAsFloat(this int value) {
		IntFloat converter = default;
		converter.intValue = value;
		return converter.floatValue;
	}
}