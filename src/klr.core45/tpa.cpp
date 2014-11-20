#include "stdafx.h"
#include "tpa.h"


// TODO: dynamically generated following method
BOOL CreateTpaBase(LPWSTR** ppNames, size_t* pcNames, bool bNative)
{
    const size_t cAssembly = 33;

    LPWSTR* pArray = new LPWSTR[cAssembly];

    int i = 0;

    if (bNative)
    {
        pArray[i++] = _wcsdup(L"System.Collections.ni.dll");
        pArray[i++] = _wcsdup(L"System.ni.dll");
        pArray[i++] = _wcsdup(L"mscorlib.ni.dll");
        pArray[i++] = _wcsdup(L"System.Core.ni.dll");
        pArray[i++] = _wcsdup(L"System.Console.ni.dll");
        pArray[i++] = _wcsdup(L"System.Diagnostics.Debug.ni.dll");
        pArray[i++] = _wcsdup(L"System.Text.Encoding.Extensions.ni.dll");
        pArray[i++] = _wcsdup(L"System.Threading.ni.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Extensions.ni.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.InteropServices.ni.dll");
        pArray[i++] = _wcsdup(L"System.Resources.ResourceManager.ni.dll");
        pArray[i++] = _wcsdup(L"System.Threading.Tasks.ni.dll");
        pArray[i++] = _wcsdup(L"System.IO.FileSystem.Primitives.ni.dll");
        pArray[i++] = _wcsdup(L"Internal.IO.FileSystem.Primitives.ni.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.ni.dll");
        pArray[i++] = _wcsdup(L"mscorlib.Extensions.ni.dll");
        pArray[i++] = _wcsdup(L"System.Text.Encoding.ni.dll");
        pArray[i++] = _wcsdup(L"System.IO.ni.dll");
        pArray[i++] = _wcsdup(L"System.IO.FileSystem.ni.dll");
        pArray[i++] = _wcsdup(L"System.Threading.ThreadPool.ni.dll");
        pArray[i++] = _wcsdup(L"System.Threading.Overlapped.ni.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Handles.ni.dll");
        pArray[i++] = _wcsdup(L"System.Linq.ni.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.ni.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Loader.ni.dll");
        pArray[i++] = _wcsdup(L"System.AppContext.ni.dll");
        pArray[i++] = _wcsdup(L"System.Collections.Concurrent.ni.dll");
        pArray[i++] = _wcsdup(L"System.Globalization.ni.dll");
        pArray[i++] = _wcsdup(L"System.Diagnostics.Tracing.ni.dll");
        pArray[i++] = _wcsdup(L"System.ComponentModel.ni.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.Extensions.ni.dll");
        pArray[i++] = _wcsdup(L"System.Text.RegularExpressions.ni.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.Primitives.ni.dll");
    }
    else
    {
        pArray[i++] = _wcsdup(L"System.Collections.dll");
        pArray[i++] = _wcsdup(L"System.dll");
        pArray[i++] = _wcsdup(L"mscorlib.dll");
        pArray[i++] = _wcsdup(L"System.Core.dll");
        pArray[i++] = _wcsdup(L"System.Console.dll");
        pArray[i++] = _wcsdup(L"System.Diagnostics.Debug.dll");
        pArray[i++] = _wcsdup(L"System.Text.Encoding.Extensions.dll");
        pArray[i++] = _wcsdup(L"System.Threading.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Extensions.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.InteropServices.dll");
        pArray[i++] = _wcsdup(L"System.Resources.ResourceManager.dll");
        pArray[i++] = _wcsdup(L"System.Threading.Tasks.dll");
        pArray[i++] = _wcsdup(L"System.IO.FileSystem.Primitives.dll");
        pArray[i++] = _wcsdup(L"Internal.IO.FileSystem.Primitives.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.dll");
        pArray[i++] = _wcsdup(L"mscorlib.Extensions.dll");
        pArray[i++] = _wcsdup(L"System.Text.Encoding.dll");
        pArray[i++] = _wcsdup(L"System.IO.dll");
        pArray[i++] = _wcsdup(L"System.IO.FileSystem.dll");
        pArray[i++] = _wcsdup(L"System.Threading.ThreadPool.dll");
        pArray[i++] = _wcsdup(L"System.Threading.Overlapped.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Handles.dll");
        pArray[i++] = _wcsdup(L"System.Linq.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.dll");
        pArray[i++] = _wcsdup(L"System.Runtime.Loader.dll");
        pArray[i++] = _wcsdup(L"System.AppContext.dll");
        pArray[i++] = _wcsdup(L"System.Collections.Concurrent.dll");
        pArray[i++] = _wcsdup(L"System.Globalization.dll");
        pArray[i++] = _wcsdup(L"System.Diagnostics.Tracing.dll");
        pArray[i++] = _wcsdup(L"System.ComponentModel.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.Extensions.dll");
        pArray[i++] = _wcsdup(L"System.Text.RegularExpressions.dll");
        pArray[i++] = _wcsdup(L"System.Reflection.Primitives.dll");
    }

    *ppNames = pArray;
    *pcNames = cAssembly;

    return true;
}


BOOL FreeTpaBase(const LPWSTR* values, const size_t count)
{
    for (size_t idx = 0; idx < count; ++idx)
    {
        delete[] values[idx];
    }

    delete[] values;

    return true;
}