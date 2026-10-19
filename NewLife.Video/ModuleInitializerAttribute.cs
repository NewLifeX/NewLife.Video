#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>模块初始化器标记。低版本框架缺少该特性，为源生成器生成的多语言注册代码提供本地定义</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute { }
#endif
