using System;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public static class Utils
    {
        public static void RemoveEntries<T>(ref T[] array, Func<T, bool> predicate)
        {
            var length = array.Length;
            for (int i = array.Length - 1; i > -1; i--)
            {
                if (!predicate(array[i]))
                    continue;
                
                length--;
                for (int j = i + 1; j < array.Length; j++)
                    array[j - 1] = array[j];
            }
            
            Array.Resize(ref array, length);
        }
    }
}