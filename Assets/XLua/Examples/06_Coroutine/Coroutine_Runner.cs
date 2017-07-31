using UnityEngine;
using XLua;
using System.Collections.Generic;
using System.Collections;
using System;

[LuaCallCSharp]
public class Coroutine_Runner : MonoBehaviour
{
    public void YieldAndCallback(object to_yield, Action callback)
    {
        Debug.Log("### 1");
        StartCoroutine(CoBody(to_yield, callback));
        Debug.Log("### 2");
    }

    private IEnumerator CoBody(object to_yield, Action callback)
    {
        Debug.Log("### 3");
        if (to_yield is IEnumerator)
        {
            Debug.Log("### 4");
            yield return StartCoroutine((IEnumerator)to_yield);
        }
        else
        {
            Debug.Log("### 5");
            yield return to_yield;
        }

        Debug.Log("### 6");
        callback();
    }
}

public static class CoroutineConfig
{
    [LuaCallCSharp]
    public static List<Type> LuaCallCSharp
    {
        get
        {
            return new List<Type>()
            {
                typeof(WaitForSeconds),
                typeof(WWW)
            };
        }
    }
}
