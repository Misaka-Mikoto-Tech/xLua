# 本文档为阅读 xlua 源码的笔记，希望可以对有需要的人提供帮助

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

- `import_type` 函数如果发现类型没有会尝试加载类型，加载过程中分两步，分别为注册 Object 和 注册 Class：
    - 注册 Object，创建一个以类型名为 key 的 metatable 注册到全局表，将类型所有的实例方法及回调函数设置为此 table 的 key value。
    - 注册 Class, 创建一个table，把自己以自己的名称为 key 设置到包含 .fqn 字段的以 `CS.` 开头的全路径对应的末级 table 中(如果有 nested class 就不是末级了)，将所有的静态方法和构造函数对应的回调函数设置进去。

        例如： 注册 GameObject 类型的 Class 时会在 CS.UnityEngine 表中增加一个字段名为 **GameObject**, 指向刚创建的 table


- xlua 对目标的对象方法查找和类方法查找走的是不同路径，对象查找是通过 metatable 来完成的，而类方法走的则是从 `CS.` 开始的多级 table，通过类方法找到某个类型(table)后，如果执行了括号操作, 将会调用 `_call` 字段对应的方法，`_call` 方法在在C#中一般被赋值为 _CreateInstance 方法，调用此方法会创建对象，并通过设置 metatable 和 objid 的方式传递给 lua 作为 userdata保存起来 => (*((int *)userdata) = objid