# 本文档为阅读 xlua 源码的笔记，希望可以对有需要的人提供帮助

## lua 与 C# 交互过程分析
- 类型的数据及getter,setter存储在lua function的upvalue里
- 如果没有生成代码，那么会直接修改IL
- 如果生成了代码，有代码生成的会被延迟加载并且注册所有的函数
- 如果执行了生成了代码，但目标类型并没有标记为生成代码，那么将会生成 Field Getter和Setter，是一个lambda表达式，使用 field.GetValue()方式通过反射获取或设置值(Utils.genFieldGetter)
- 给lua set数据会调用 PushByType, 如果没有Type会调用通用的 Push, 通用 push 同样会找不到type，则会立即调用Wrap来生成调用代码(DelayLoad or 生成 lambda 反射代码)   {xlua依赖类型系统}

- lua 传递函数给 C# 代理时，会new一个delegateBridge 作为target，同时记录lua函数对应的ref(push -1,new Bridge(ref,L), method通过代码生成或其它手段指定，调用时执行method，push ref(lua函数)并call

- lua 传递 Table 到C#时，C#定义一个 interface, 然后可以被自动 GenCaster并且设置到对应的对象（代码生成的InterfaceBridge）

- 一个 lua 函数对应一个 DelegateBridge，如果多个C# 代理 链接到此lua函数，则都使用同一个DelegateBridge，同时根据代理类型存储进Dictionary里

- lua table映射到 class 的方式是通过遍历class所有字段然后去table里查找，查找到后通过 field.SetValue 等反射方式设置值的

- 传递 C# 对象到 lua 时，调用 translator.push 压栈对象，如果没有找到缓存则首先缓存对象并返回一个index，然后调用xlua的xlua_pushcsobj将index, typeid传入，并设置缓存为true，xlua对应代码则创建一个userdata,将index存储于其中，并将typeid对应的metatable设置为userdata的metatable，若cache参数为true，则添加一个index到userdata的映射,此映射的用途是当C#里要push一个对象时，如果此对象之前已经push过，则可以迅速通过index找到对应的userdata并push(xlua_tryget_cachedud)

- 已经存在的对象在C#端和lua端各有一个表进行存储，C#端是reverseMap(obj, int), lua端是cacheRef(int, userdata)

- xlua 存储对象是使用的 userdata，userdata指针就存储了一个值，是C#里的translator.objects对应的一个编号，C#根据这个编号去取对应的对象，lua里方法调用时把self(userdata)压栈,因此C#中可以取到对应的值

- 对于 lua 代码中以 `CS.` 开头的类型查找使用通用查找逻辑，如果没有缓存会调用 `import_type` 进行查找，如果还是没有找到则创建一个obj，其中有一个字段(.fqn),并且设置 metatable 为通用查找 table，如果找到了(loaded_type存在或者调用delayloader或者reflectionWrap成功)则返回true，如果没有找到则lua会创建一个table，并且其.fqn字段是其父类路径，并且返回，下一级路径查找时object就是之前创建的table，传递给 `import_type` 的路径是与 .fqn 组合起来的全路径

- `import_type` 函数如果没有找到类型会尝试加载类型（调用生成的代码或者执行反射），加载过程中分两步，分别为注册 Object 和 注册 Class：

> 1. 注册 Object，创建一个以类型名为 key 的 metatable① 注册到全局表，将类型所有的实例方法名及回调函数设置为此 table 的 key value。
> 2. 注册 Class, 创建一个table，把自己以自己的名称为 key 设置到包含 .fqn 字段的以 `CS.` 开头的全路径对应的末级 table② 中(如果有 nested class 就不是末级了)，将所有的静态方法名和构造函数对应的回调函数设置进去。

>> `例如： 注册 GameObject 类型的 Class 时会在 CS.UnityEngine 表中增加一个字段, 名为 `GameObject`, 指向刚创建的 table②`

- xlua 对目标的对象方法查找和类方法查找走的是不同路径，对象查找是通过 metatable① 来完成的，而类方法走的则是从 `CS.` 开始的多级 table②，通过类方法找到某个类型(table)后，如果执行了括号操作, 将会调用 `_call` 字段对应的方法，`_call` 方法在在C#中一般被赋值为 _CreateInstance 方法，调用此方法会创建对象，并通过设置 metatable 和 objid 的方式传递给 lua 作为 userdata保存起来 => (*((int *)userdata) = objid
 

-----
## Inject HotFix Opcode的原理分析
- 所有需要注入的函数都需要有与之定义对应的 delegatebridge 里的方法匹配用来把参数传递给lua并call，比如非静态无参方法使用此方法 `void __Gen_Delegate_Imp15(object p0)`， 生成代码时会把标记为 hotfix 的类里面的所有方法都生成对应签名的wrap方法, DelegateBridge.GetDelegateByType 方法没有列出来的都是为hotfix生成的

- 函数体被加入一个 DelegateBridge 类型的局部变量

- 定义一个独一无二名字的 DelegateBridge 类型的静态字段，名字规则 `__Hotfix{overload}_{method_name}`, 每个函数最多支持100个重载，

- 如果是构造函数则遍历每一个ret，在之前都注入代码，否则在函数体第一条指令前插入

- 把静态字段复制到局部变量，判断变量值是否为空，如果不为空就执行 hotfix 逻辑

-  self/this 参数的类型：如果是StateFull，需要luatable类型的参数，否则如果是值类型那么需要与之匹配的值类型，否则全部是 System.Object

-  处理泛型参数和返回值

-  压栈 self 以及所有参数，call 对应的方法 `__Gen_Delegate_Imp15`, 最后直接 ret-  附IL
```
  .method private hidebysig instance void Update() cil managed
{
    .maxstack 3
    .locals init (
        [0] int32 num,
        [1] class XLua.DelegateBridge bridge) // 使用一个局部变量
    
    // 这是添加的一个当前类的 Field,类型与上面变量相同
    L_0000: ldsfld class XLua.DelegateBridge HotfixTest::__Hotfix0_Update
    L_0005: stloc bridge   // 将字段复制到局部变量
    L_0009: ldloc bridge   // 压栈局部变量
    L_000d: brfalse L_001d // 判断此变量是否为 null
    L_0012: ldloc bridge
    L_0016: ldarg.0   // 压栈 this
    // 调用参数与 Update 匹配的 wrap 函数
    L_0017: call instance void XLua.DelegateBridge::__Gen_Delegate_Imp15(object)
    L_001c: ret  // 直接跳走，不再执行原有逻辑
    L_001d: ldarg.0      // 原始函数入口
    L_001e: dup 
    L_001f: ldfld int32 HotfixTest::tick
    L_0024: ldc.i4.1 
    L_0025: add 
    L_0026: dup 
    L_0027: stloc.0 
    L_0028: stfld int32 HotfixTest::tick
    L_002d: ldloc.0 
    L_002e: ldc.i4.s 50
    L_0030: rem 
    L_0031: brtrue L_0050
    L_0036: ldstr ">>>>>>>>Update in C#, tick = "
    L_003b: ldarg.0 
    L_003c: ldfld int32 HotfixTest::tick
    L_0041: box int32
    L_0046: call string [mscorlib]System.String::Concat(object, object)
    L_004b: call void [UnityEngine]UnityEngine.Debug::Log(object)
    L_0050: ret 
}
```

## xlua.hotfix 函数执行逻辑
- 调用 xlua.hotfix 会执行C#里的XLuaAccess方法100遍，找指定名称的字段或者属性，找到后就给它设置为lua函数的warpper（可能是一个 DelegateBridge）

- 多个C#函数的重载都映射到同样的lua hotfix 函数，只是参数不一样

- 如果使用了 IntKey, 那么执行 hotfix 时将不再执行100遍，因为它们有了严格的对应关系，但是由于IDE与发布时的代码可能不同，函数序号可能对应不起来，此问题的处理方案是把映射文件进行动态下载。[官方文档对此问题的解释](https://github.com/Tencent/xLua/blob/master/Assets/XLua/Doc/hotfix.md#hotfix-flag)
 
 

## delegate 被置空 LuaEnv 才不会报错的原理
1. 根本原因是 `Dispose` 会调用 `translator.AllDelegateBridgeReleased` 检查 `delegate_bridges` 中是否还有存活的对象，如果有存活的对象即报错误。
2. 有两种表示对象被释放了，第一种是 `delegateBridge` 中某对象已没有引用者，那么它的 IsAlive 则为false，表示对象可以被移除，第二种是在第一种的基础上在之前的LuaEnv.Tick函数中被检查到并且从被列表中删除了)
3. `Dispose`函数会调用 `System.GC.Collect()` 以及 `System.GC.WaitForPendingFinalizers()` 强制回收所有垃圾，如果某个 Delegate 被置空则会被回收并被调用目标的析构函数。另外由于执行了这两句代码，上述 ==2.== 中的第一种情况就不存在了，因为没有引用的对象一定被执行了析构函数。
4. `DelegateBridge` 的基类 `LuaBase` 的析构函数调用的 `Dispose` 函数会把自己引用的 `luaRef` 添加进 `refQueue` 队列等待被删除。
5. `LuaEnv.Tick`函数的逻辑是会将 `refQueue` 队列中的所有待移除引用在 lua 端删除，如果是 delegate 的话还会将自己从 `translator.delegate_bridges` 列表中移除。
7. 疑问：如果 LuaEnv 是游戏退出时才释放，是否还需要这些操作？


 ## 注意事项
 - 一旦执行了 **XLua/Hotfix Inject In Editor** 将不可再使用 vs 调试，但是出错时 unity ide 自身显示的出错行数还是正确的，原因是 Mono.Cecil 处理IL指令时会同时修改与之关联 mdb 文件，但 vs 使用的 pdb 文件并没有被同步修改
