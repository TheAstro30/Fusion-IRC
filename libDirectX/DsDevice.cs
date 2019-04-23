/******************************************************
                  DirectShow .NET
		      netmaster@swissonline.ch
*******************************************************/
//					DsDevice
// enumerate directshow devices and moniker handling
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace libDirectX
{
    [ComVisible(false)]
    public class DsDev
    {
        public static bool GetDevicesOfCat(Guid cat, out ArrayList devs)
        {
            devs = null;
            object comObj = null;
            ICreateDevEnum enumDev = null;
            UCOMIEnumMoniker enumMon = null;
            var mon = new UCOMIMoniker[1];
            try
            {
                var srvType = Type.GetTypeFromCLSID(Clsid.SystemDeviceEnum);
                if (srvType == null)
                {
                    throw new NotImplementedException("System Device Enumerator");
                }
                comObj = Activator.CreateInstance(srvType);
                enumDev = (ICreateDevEnum) comObj;
                var hr = enumDev.CreateClassEnumerator(ref cat, out enumMon, 0);
                if (hr != 0)
                {
                    throw new NotSupportedException("No devices of the category");
                }
                var count = 0;
                do
                {
                    int f;
                    hr = enumMon.Next(1, mon, out f);
                    if ((hr != 0) || (mon[0] == null))
                    {
                        break;
                    }
                    var dev = new DsDevice {Name = GetFriendlyName(mon[0])};
                    if (devs == null)
                    {
                        devs = new ArrayList();
                    }
                    dev.Mon = mon[0];
                    mon[0] = null;
                    devs.Add(dev);
                    count++;
                }
                while (true);

                return count > 0;
            }
            catch (Exception)
            {
                if (devs != null)
                {
                    foreach (DsDevice d in devs)
                    {
                        d.Dispose();
                    }
                    devs = null;
                }
                return false;
            }
            finally
            {
                if (mon[0] != null)
                {
                    Marshal.ReleaseComObject(mon[0]);
                }
                mon[0] = null;
                if (enumMon != null)
                {
                    Marshal.ReleaseComObject(enumMon);
                }
                if (comObj != null)
                {
                    Marshal.ReleaseComObject(comObj);
                }
            }
        }

        private static string GetFriendlyName(UCOMIMoniker mon)
        {
            object bagObj = null;
            IPropertyBag bag;
            try
            {
                var bagId = typeof (IPropertyBag).GUID;
                mon.BindToStorage(null, null, ref bagId, out bagObj);
                bag = (IPropertyBag) bagObj;
                object val = "";
                var hr = bag.Read("FriendlyName", ref val, IntPtr.Zero);
                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);
                var ret = val as string;
                if (string.IsNullOrEmpty(ret))
                {
                    throw new NotImplementedException("Device FriendlyName");
                }
                return ret;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (bagObj != null)
                {
                    Marshal.ReleaseComObject(bagObj);
                }
            }
        }
    }

    [ComVisible(false)]
    public class DsDevice : IDisposable
    {
        public UCOMIMoniker Mon;
        public string Name;

        public void Dispose()
        {
            if (Mon != null)
                Marshal.ReleaseComObject(Mon);
            Mon = null;
        }
    }

    [ComVisible(true), ComImport,
     Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator(
            [In] ref Guid pType,
            [Out] out UCOMIEnumMoniker ppEnumMoniker,
            [In] int dwFlags);
    }

    [ComVisible(true), ComImport,
     Guid("55272A00-42CB-11CE-8135-00AA004BB851"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyBag
    {
        [PreserveSig]
        int Read(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref object pVar,
            IntPtr pErrorLog);

        [PreserveSig]
        int Write(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
            [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
    }
}