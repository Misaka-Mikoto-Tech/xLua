# 本文档为阅读 xlua 源码的笔记，希望可以对有需要的人提供帮助

- 类型的数据及getter,setter存储在lua function的upvalue里
- 如果没有生成代码，那么会直接修改IL
- 如果生成了代码，有代码生成的会被延迟加载并且注册所有的函数
- 如果生成了代码，但目标类型并没有生成代码，那么将会生成 FieldGetter和Setter，是一个lambda表达式，使用 field.GetValue()方式通过反射获取或设置值(Utils.genFieldGetter)
- 给lua set数据会调用 PushByType, 如果没有Type会调用通用的 Push, 通用 push 同样会找不到type，则会立即调用Wrap来生成调用代码(DelayLoad or 生成 lambda 反射代码)   {xlua依赖类型系统}

- 将lua函数赋值给C#代理时，会new一个delegateBridge 作为target，同时记录lua函数对应的ref(push -1,new Bridge(ref,L),method通过代码生成或其它手段指定，调用时执行method，push ref(lua函数)并call

- lua 传递 Table 到C#时，C#定义一个 interface, 然后可以被自动 GenCaster并且设置到对应的对象（代码生成的InterfaceBridge）

- 一个 lua 函数对应一个 DelegateBridge，如果多个C# 代理 链接到此lua函数，则都使用同一个DelegateBridge，同时根据代理类型存储进Dictionary里

- lua table映射到 class 的方式是通过遍历class所有字段然后去table里查找，查找到后通过 field.SetValue 等反射方式设置值的

- xlua 创建一个C#对象时，调用 translator.push 压栈对象，push如果没有找到缓存则首先缓存对象并返回一个index，然后调用xlua的xlua_ _pushcsobj将index, typeid传入，并设置缓存为true，xlua对应代码则创建一个userdata,将index存储于其中，并将typeid对应的metatable设置为userdata的metatable，若cache参数为true，则添加一个index到userdata的映射,此映射的用途是当C#里要push一个对象时，如果此对象之前已经push过，则可以迅速通过index找到对应的userdata并push(xlua_tryget_cachedud)

- 已经存在的对象在C#端和lua端各有一个表进行存储，C#端是reverseMap(obj, int), lua端是cacheRef(int, userdata)

- xlua 存储对象是使用的 userdata，userdata指针就存储了一个值，是C#里的translator.objects对应的一个编号，C#根据这个编号去取对应的对象，lua里方法调用时把self(userdata)压栈,因此C#中可以取到对应的值
- 对于lua中CS开头的目标查找，如果没有缓存会调用import_type进行查找，如果还是没有找到则创建一个obj，其中只有一个字段(.fqn),并且设置metatable，如果找到了(loaded_type存在或者调用delayloader或者reflectionWrap成功)则返回TRUE，并且此时lua中一定有此类型对应的metatable,也就是说在xlua的路径查找过程中，如果没有找到则lua会创建一个table，并且其.fqn字段是其父类路径，并且返回，下一级路径查找时object就是之前创建的table，传递给import_type的路径是与 .fqn 组合起来的全路径

- C#注册一个类的过程，首先是创建一个 metatable,然后注册一堆的方法（registerObject），然后开始注册Class，注册Class的过程是创建一个table，这个table注册一堆方法和 _index, _newIndex, 然后把注册的方法都设置为upvalue,然后将这个table 设置为Type的全路径table的一个字段，比如设置 GameObject 时会在CS.UnityEngine 表中增加一个字段 GameObject,指向此table
- xlua wrap 目标的对象方法查找和类方法查找走的是不同路径，对象查找是通过metatable来完成的，而类方法走的则是从CS开始的多级table，而通过类方法找到某个类型(table)后，调用 _call 方法，在C#中执行 _CreateInstance 方法，创建对象，并通过设置metatable的方式传递给lua作为 userdata保存起来