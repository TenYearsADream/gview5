using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using gView.Framework.Data;
using gView.Framework.Geometry;
using gView.Framework.Symbology;
using gView.Framework.Carto;
using gView.Framework.IO;

/// <summary>
/// The <c>gView.Framework</c> provides all interfaces to develope
/// with and for gView
/// </summary>
namespace gView.Framework.system
{
	/// <summary>
	/// In the <c>gView.Framework</c> namespace all interfaces are defined. If you want to develope your own extensions use this interfaces.
	/// Extensions can be made with any interfaces that implement <see cref="gView.Framework.IgViewExtension"/> 
	/// </summary>
	internal class NamespaceDoc 
	{
	}

    [global::System.AttributeUsageAttribute(global::System.AttributeTargets.Class)]
    public class RegisterPlugIn : global::System.Attribute
    {
        private Guid _guid;
        public RegisterPlugIn(string guid)
        {
            _guid = new Guid(guid);
        }
        public Guid Value
        {
            get { return _guid; }
        }
    }

    public interface IPlugInWrapper
    {
        object WrappedPlugIn { get; }
    }

    public interface IPlugInParameter
    {
        object Parameter { get; set; }
    }

    public interface IPlugInDependencies
    {
        bool HasUnsolvedDependencies();
    }

    public interface IMapApplicationModule
    {
        void OnCreate(Object hook);
    }

	public interface ICancelTracker 
	{
		void Cancel();
        void Pause();
		bool Continue { get; }
        bool Paused { get; }
	}

    public class ListOperations<T>
    {
        static public List<T> Clone(List<T> l)
        {
            if (l == null) return null;
            List<T> c = new List<T>();
            
            foreach (T e in l)
                c.Add(e);

            return c;
        }

        static public List<T> Swap(List<T> l)
        {
            List<T> s = new List<T>();
            for (int i = l.Count - 1; i >= 0; i--)
                s.Add(l[i]);

            return s;
        }

        static public List<T> Sort(List<T> l, IComparer<T> c)
        {
            // Die Routine wird zum Beispiel zum Sortieren von
            // LabelLayers nach der Priorität verwendet.
            //
            // Besonders bei dieser Sortierung ist, dass die ursprüngliche
            // Reihenfolge unverändert bleicht, wenn der Comparer 0 liefert.
            // Beim normal List.Sort ist das nicht unbedingt der Fall...
            //
            if (l == null || c == null) return l;

            try
            {
                List<T> ret = new List<T>();
                foreach (T o in l)
                {
                    int pos = 0;
                    foreach (T r in ret)
                    {
                        if (c.Compare(r, o) < 0)
                        {
                            break;
                        }
                        pos++;
                    }
                    ret.Insert(pos, o);
                }

                return ret;
            }
            catch
            {
                return l;
            }
        }
    }

    public interface IClone 
	{
		object Clone();
	}

    public interface IClone2
    {
        object Clone(IDisplay display);
        void Release();
    }

    public interface IClone3
    {
        object Clone(IMap map);
    }

    public interface IClone4
    {
        object Clone(Type type);
    }

    public interface IErrorMessage
    {
        string lastErrorMsg { get; }
    }

    public enum loggingMethod { request, request_detail, error, request_detail_pro }

    public enum LicenseTypes
    {
        Express = 0,
        Licensed = 1,
        Expired = 2,
        Unknown = 3
    }

    public interface ILicense
    {
        LicenseTypes ComponentLicenseType(string componentName);
        int DurationDaysLeft { get; }
        string ProductID
        {
            get;
        }
        LicenseTypes LicenseType
        {
            get;
        }
        string ProductName
        {
            get;
        }

        string LicenseFile { get; }
        bool LicenseFileExists { get; }
        string[] LicenseComponents { get; }
    }

    public interface IRefreshable
    {
        void RefreshFrom(object obj);
    }

    public interface IDirtyEvent
    {
        event EventHandler DirtyEvent;
    }

    public interface IPriority
    {
        int Priority { get; }
    }

    public interface IIdentity
    {
        string UserName { get; }
        List<string> UserRoles { get; }
        string HashedPassword { get; }
    }

    public interface IUserData
    {
        void SetUserData(string type, object val);
        void RemoveUserData(string type);
        object GetUserData(string type);
        string[] UserDataTypes { get; }
        void CopyUserDataTo(IUserData userData);
    }

    public interface IInitializeClass
    {
        void Initialize(object parameter);
    }

    public interface ITimeEvent
    {
        string Name { get; }
        DateTime StartTime { get; }
        DateTime FinishTime { get; }
        TimeSpan Duration { get; }
        int Counter { get; }
    }

    public delegate void TimeEventAddedEventHandler(ITimeStatistics sender,ITimeEvent timeEvent);
    public delegate void TimeEventsRemovedEventHandler(ITimeStatistics sender);
    public interface ITimeStatistics
    {
        event TimeEventAddedEventHandler TimeEventAdded;
        event TimeEventsRemovedEventHandler TimeEventsRemoved;

        void RemoveTimeEvents();
        void AddTimeEvent(ITimeEvent timeEvent);
        void AddTimeEvent(string name, DateTime startTime, DateTime finishTime);

        List<ITimeEvent> TimeEvents { get; }
    }

    public interface INamespace
    {
        string Namespace { get; set; }
    }

    public interface IContext
    {
    }
    public class Context : IContext
    {
    }

    public interface ICount
    {
        int Count { get; }
    }

    public interface IOnLoadObject
    {
        void OnLoadObject(object parameter);
    }

    public interface ISimpleNumberCalculation : IPersistable
    {
        string Name { get; }
        string Description { get; }

        double Calculate(double val);
    }

    public interface IDebugging
    {
        Exception LastException { get; set; }
    }

    public interface ISimplify
    {
        void Simplify();
    }

    public interface IObjectWrapper
    {
        object WrappedObject { get; }
    }
}
