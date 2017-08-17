## C# 编写日常操作
>  总的来说，日常如果无论是写C#代码还是写lua代码都不需要做任何操作，菜单栏 XLua 下的三个命令也不需要点

> **但还是有几点需要说明：**
- 请仔细阅读 XLuaTypeConfig.cs 这个文件，特别需要关注带有 `[XLua.ReflectionUse]` 标签的几个字段，一般来说，C#内没有用到但 lua **可能**会用到的，全部都需要添加在这里,否则在 IL2CPP 会被裁剪掉导致lua无法访问。
- 如果需要使用泛型，尽量使用泛型方法而不是泛型类。
- 编写泛型方法时，请务必做到以下几点:
    1. 泛型参数必须有 where 约束，并且约束的条件必须是 class, 不能只有 interface。
    2. 形参必须直接使用到泛型参数，比如 
        ```csharp
            public void FuncA<T>(T argT) {}
        ```
        这样就是OK的, 而不使用泛型参数或者使用嵌套类型比如这样的
        ```csharp
            public void FuncB<T>(List<T> argLstT) {}
        ```
        是不行的, 实在不行的话可以加一个 dummy 参数，比如
        ```csharp
            public void FuncB<T>(List<T> argLstT, T __dummyT = default(T)) {}
        ```
- 如果使用了泛型代理，比如 `Action<int, object>`, 请把它加在文件 XLuaTypeConfig.cs 中带有`[XLua.CSharpCallLua]`标签的字段内(或者自己加一个字段)，否则C#无法根据类型找到对应的 DelegateBridge 中的 wrap 方法



## Lua 编写日常操作
1. ### Hotfix 操作
    - 在 `Assets\3rd\XLua\Resources\@LuaSrc\HotFix` 文件夹内新建一个以被 hotfix 类名为文件名，扩展名为 `.lua.txt` 的文件。
    - 在其中编写代码举例如下:
        ```lua
            -- 如果加上这一行那么 lua 可以访问此类中的私有成员和方法, 本质上是重新生成了以反射方式调用的 wrap
            -- 如果之前有进行代码生成，那么生成的代码将无效
            xlua.private_accessible(CS.LoginRole)
            xlua.hotfix(CS.LoginRole, 'get_IosCheckServer',  -- 声明需要 hotfix 的函数，如果需要 hotfix 多个函数，请参考官方文档
                function(self)
                    local b = self.m_IosCheckServer
                    print("<color=#89DbC4>Call LoginRole.IosCheckServer->[" .. tostring(b) .. "] from lua </color>")
                    return b
                end
            )
        ```
    - 打开文件 `Assets\3rd\XLua\Resources\@LuaSrc\hotfixReg.lua.txt`, 在其中添加一行注册代码举例如下:
        ```lua
        require 'hotfix.LoginRole'
        ```
    - 快速查看 hotfix 效果可在 Unity Editor 内执行以下操作：
        - 执行 `[XLua/Clear Generated Code]`
        - 执行 `[XLua/Generate Code]`
        - 执行 `[XLua/Hotfix Inject In Editor]`
        - 以上每一步都需要等待 Enity Editor 右下角的菊花停止旋转后才可以继续下一步
        - 执行第三步后 C# 代码将无法设置断点，如需调试 C# 请随便修改 C# 代码或者执行第一步清除生成的代码让  Unity 重新编译 dll 即可
        
        
        
2. ### 重写整个UI或者新增UI操作
    - 在 `Assets\3rd\XLua\Resources\@LuaSrc\UI` 文件夹内新建一个以 `ViewClass` 名为文件名，扩展名为 `.lua.txt` 的文件。
    - 在其中编写代码，请参考示例，注意事项如下:
        - 需要暴露给 C# 调用的函数请不要加 local 前缀，可用的函数在示例文件尾部有列出。
        - UIBase 内的方法调用和字段属性访问请一律加 this 前缀。
        - C# 内的类型访问请统一加 CS 前缀，例如创建一个 GameObject 对象可用以下代码:
            ```lua
            local newObj = CS.UnityEngine.GameObject()
            ```
    - 打开文件 `Assets\3rd\XLua\Resources\@LuaSrc\uiReg.lua.txt`, 在其头部的 table 中添加一行举例如下：
        ```lua
        local allUIs = {
            <your ViewClass name> = 'UI/<刚才创建的文件不带扩展名>',
        }
        ```
    - **使用 Lua 重写整个 UI 时翻译对应的 C# 代码需注意以下几点(有几条与上面相同):**
        1. C# 的 **else if** 一定注意改成 **elseif**, 否则会 lua block 前后不能匹配
        2. 所有 C# 端的类型都从 CS. 开始
        3. UIBase 内的方法都加 this: 前缀
        4. C# 端的 IEnumerator 请使用 util.cs_generator 创建
        5. local 变量如果想让其它函数访问，请将其声明在被调用函数之前(也可以都放在文件顶部)
        6. C# 端的 List，Dictionary 类型的参数在 Lua 端通通使用 table 传递
        7. lua table 的索引是从1开始的，所以请把代码里的索引全部 +1
        8. C# 端的泛型方法请注意尽量编写的可以被 lua 自动调用(泛型参数有类约束，参数列表内必须直接使用泛型参数)
        9. 如果要在lua内访问目标类型的私有成员，请添加 xlua.private_accessible(CS.类型名)
        10. 对于C#端的 Delegate 以及 MulticastDelegate 类型的参数，必须将其转化为 C# 端的固定类型的 delegate         对象，比如 Action, 否则是不会被识别的(参见 PlayerView.lua.txt line 94)
        11. 如果执行 typeof(Delegate/MulticastDelegate).GetMethod("Invoke") 会导致 Unity Crash，请务必小心
        12. require 加载的文件的环境块始终是 _G, 如果想要代码在指定的环境块中执行请参考 uiReg.lua.txt 中的            xlua.CreateLuaUI 函数

3. ### 打包操作
这个我没有测试充分，但是也有几点需要注意：

- 为了提高性能并且减小可执行文件增量，本项目采用了 IntKey 的方式生成 hotfix，也就是 hotfix 判断并不是判断的方法名而是整数值，但刚才我们 hotfix 时都是使用的方法名，因此需要有个文件来进行方法名和整数值的映射，这个文件位于 `Assets\3rd\XLua\Gen\Resources\` 下名为 `hotfix_id_map.lua.txt`，为了安全 XLua 每次都会备份之前的文件，请妥善保管这些文件（可以考虑打包时把旧文件剪切到其它位置）

- 由于最终打包前插桩操作时 unity 已经不接受新文件，所以在打包脚本中在切换到指定平台生成 APK 前提前执行了一遍保证资源目录中已经有此文件,请不要去掉此操作(但可以把其它备份文件移除)。

- iOS 由于裁剪可能会造成诸多代码无法访问，写完 lua 后请多测试，遇到不能操作的请把类型加到文件 XLuaTypeConfig.cs 中 带`[XLua.ReflectionUse]` 的字段中去。


4. ### 其它
    - 由于 xlua 生成器的 bug，对于包含扩展方法的类，也必须加到 ReflectionUse 里, 否则如果对目标类型执行了 xlua.private_accessible 后扩展方法会找不到，这跟官方文档的说明不相符，原因是生成反射wrap时对于哪些类型包含扩展方法 xlua 只检查了包含 ReflectionUse 属性的列表，而官方文档说 ReflectionUse 或者 LuaCallCSharp 都可以（目测作者很快就会修复）