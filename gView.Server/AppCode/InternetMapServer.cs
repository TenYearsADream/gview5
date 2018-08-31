﻿using gView.Framework.Carto;
using gView.Framework.Data;
using gView.Framework.IO;
using gView.Framework.system;
using gView.MapServer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace gView.Server.AppCode
{
    class InternetMapServer
    {
        static public ServerMapDocument MapDocument = new ServerMapDocument();
        static public ThreadQueue<IServiceRequestContext> ThreadQueue = null;
        static internal string ServicesPath = String.Empty;
        static private int _port = 0;
        static internal string OutputPath = String.Empty;
        static internal string OutputUrl = String.Empty;
        static internal string TileCachePath = String.Empty;
        static internal List<IServiceRequestInterpreter> _interpreters = new List<IServiceRequestInterpreter>();
        static internal License myLicense = null;
        static internal List<IMapService> mapServices = new List<IMapService>();
        static internal MapServerInstance Instance = null;
        static internal Acl acl = null;

        static public void Init(string folder, int port = 80)
        {
            Instance = new MapServerInstance(80);

            ServicesPath = folder;
            foreach (var mapFileInfo in new DirectoryInfo(folder.ToPlattformPath()).GetFiles("*.mxl"))
            {
                try
                {
                    MapService service = new MapService(mapFileInfo.FullName, MapServiceType.MXL);
                    mapServices.Add(service);
                    Console.WriteLine("service " + service.Name + " added");
                }
                catch (Exception ex)
                {
                    Logger.Log(loggingMethod.error, "LoadConfig - " + mapFileInfo.Name + ": " + ex.Message);
                }
            }
        }

        internal static void LoadConfigAsync()
        {
            Thread thread = new Thread(new ThreadStart(LoadConfig));
            thread.Start();
        }
        internal static void LoadConfig()
        {
            try
            {
                if (MapDocument == null) return;

                DirectoryInfo di = new DirectoryInfo(ServicesPath);
                if (!di.Exists) di.Create();

                acl = new Acl(new FileInfo(ServicesPath + @"\acl.xml"));

                foreach (FileInfo fi in di.GetFiles("*.mxl"))
                {
                    try
                    {
                        if (mapServices.Count < Instance.MaxServices)
                        {
                            MapService service = new MapService(fi.FullName, MapServiceType.MXL);
                            mapServices.Add(service);
                            Console.WriteLine("service " + service.Name + " added");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(loggingMethod.error, "LoadConfig - " + fi.Name + ": " + ex.Message);
                    }
                }
                foreach (FileInfo fi in di.GetFiles("*.svc"))
                {
                    try
                    {
                        if (mapServices.Count < Instance.MaxServices)
                        {
                            MapService service = new MapService(fi.FullName, MapServiceType.SVC);
                            mapServices.Add(service);
                            Console.WriteLine("service " + service.Name + " added");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(loggingMethod.error, "LoadConfig - " + fi.Name + ": " + ex.Message);
                    }
                }

                try
                {
                    // Config Datei laden...
                    FileInfo fi = new FileInfo(ServicesPath + @"\config.xml");
                    if (fi.Exists)
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(fi.FullName);

                        #region onstart - alias
                        foreach (XmlNode serviceNode in doc.SelectNodes("MapServer/onstart/alias/services/service[@alias]"))
                        {
                            string serviceName = serviceNode.InnerText;
                            MapService ms = new MapServiceAlias(
                                serviceNode.Attributes["alias"].Value,
                                serviceName.Contains(",") ? MapServiceType.GDI : MapServiceType.SVC,
                                serviceName);
                            mapServices.Add(ms);
                        }
                        #endregion

                        #region onstart - load

                        foreach (XmlNode serviceNode in doc.SelectNodes("MapServer/onstart/load/services/service"))
                        {
                            ServiceRequest serviceRequest = new ServiceRequest(
                                serviceNode.InnerText, String.Empty);

                            ServiceRequestContext context = new ServiceRequestContext(
                                Instance,
                                null,
                                serviceRequest);

                            IServiceMap sm = Instance[context];

                            /*
                            // Initalisierung...?!
                            sm.Display.iWidth = sm.Display.iHeight = 50;
                            IEnvelope env = null;
                            foreach (IDatasetElement dsElement in sm.MapElements)
                            {
                                if (dsElement != null && dsElement.Class is IFeatureClass)
                                {
                                    if (env == null)
                                        env = new Envelope(((IFeatureClass)dsElement.Class).Envelope);
                                    else
                                        env.Union(((IFeatureClass)dsElement.Class).Envelope);
                                }
                            }
                            sm.Display.ZoomTo(env);
                            sm.Render();
                             * */
                        }
                        #endregion

                        Console.WriteLine("config.xml loaded...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(loggingMethod.error, "LoadConfig - config.xml: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }

        internal static IMap LoadMap(string name, IServiceRequestContext context)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(ServicesPath);
                if (!di.Exists) di.Create();

                FileInfo fi = new FileInfo(ServicesPath + @"\" + name + ".mxl");
                if (fi.Exists)
                {
                    ServerMapDocument doc = new ServerMapDocument();
                    doc.LoadMapDocument(fi.FullName);

                    if (doc.Maps.Count == 1)
                    {
                        ApplyMetadata(doc.Maps[0] as Map);
                        if (!MapDocument.AddMap(doc.Maps[0]))
                            return null;

                        return doc.Maps[0];
                    }
                    return null;
                }
                fi = new FileInfo(ServicesPath + @"\" + name + ".svc");
                if (fi.Exists)
                {
                    XmlStream stream = new XmlStream("");
                    stream.ReadStream(fi.FullName);
                    IServiceableDataset sds = stream.Load("IServiceableDataset", null) as IServiceableDataset;
                    if (sds != null && sds.Datasets != null)
                    {
                        Map map = new Map();
                        map.Name = name;

                        foreach (IDataset dataset in sds.Datasets)
                        {
                            if (dataset is IRequestDependentDataset)
                            {
                                if (!((IRequestDependentDataset)dataset).Open(context)) return null;
                            }
                            else
                            {
                                if (!dataset.Open()) return null;
                            }
                            //map.AddDataset(dataset, 0);

                            foreach (IDatasetElement element in dataset.Elements)
                            {
                                if (element == null) continue;
                                ILayer layer = LayerFactory.Create(element.Class, element as ILayer);
                                if (layer == null) continue;

                                map.AddLayer(layer);

                                if (element.Class is IWebServiceClass)
                                {
                                    if (map.SpatialReference == null)
                                        map.SpatialReference = ((IWebServiceClass)element.Class).SpatialReference;

                                    foreach (IWebServiceTheme theme in ((IWebServiceClass)element.Class).Themes)
                                    {
                                        map.SetNewLayerID(theme);
                                    }
                                }
                                else if (element.Class is IFeatureClass && map.SpatialReference == null)
                                {
                                    map.SpatialReference = ((IFeatureClass)element.Class).SpatialReference;
                                }
                                else if (element.Class is IRasterClass && map.SpatialReference == null)
                                {
                                    map.SpatialReference = ((IRasterClass)element.Class).SpatialReference;
                                }
                            }
                        }
                        ApplyMetadata(map);

                        if (!MapDocument.AddMap(map))
                            return null;
                        return map;
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }

            return null;
        }
        private static void ApplyMetadata(Map map)
        {
            try
            {
                if (map == null) return;
                FileInfo fi = new FileInfo(ServicesPath + @"\" + map.Name + ".meta");

                IServiceMap sMap = new ServiceMap(map, Instance);
                XmlStream xmlStream;
                // 1. Bestehende Metadaten auf sds anwenden
                if (fi.Exists)
                {
                    xmlStream = new XmlStream("");
                    xmlStream.ReadStream(fi.FullName);
                    sMap.ReadMetadata(xmlStream);
                }
                // 2. Metadaten neu schreiben...
                xmlStream = new XmlStream("Metadata");
                sMap.WriteMetadata(xmlStream);

                if (map is Metadata)
                    ((Metadata)map).Providers = sMap.Providers;

                // Overriding: no good idea -> problem, if multiple instances do this -> killing the metadata file!!!
                //xmlStream.WriteStream(fi.FullName);
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }
        static public void SaveConfig(IMap map)
        {
            try
            {
                if (MapDocument == null) return;

                ServerMapDocument doc = new ServerMapDocument();
                if (!doc.AddMap(map))
                    return;

                XmlStream stream = new XmlStream("MapServer");
                stream.Save("MapDocument", doc);

                stream.WriteStream(ServicesPath + @"\" + map.Name + ".mxl");
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }

        static public void SaveServiceableDataset(IServiceableDataset sds, string name)
        {
            try
            {
                if (sds != null)
                {
                    XmlStream stream = new XmlStream("MapServer");
                    stream.Save("IServiceableDataset", sds);

                    stream.WriteStream(ServicesPath + @"\" + name + ".svc");

                    if (sds is IMetadata)
                    {
                        stream = new XmlStream("Metadata");
                        ((IMetadata)sds).WriteMetadata(stream);
                        stream.WriteStream(ServicesPath + @"\" + name + ".svc.meta");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }

        static public void SaveServiceCollection(string name, XmlStream stream)
        {
            stream.WriteStream(ServicesPath + @"\" + name + ".scl");
        }

        static public bool RemoveConfig(string mapName)
        {
            try
            {
                FileInfo fi = new FileInfo(ServicesPath + @"\" + mapName + ".mxl");
                if (fi.Exists) fi.Delete();
                fi = new FileInfo(ServicesPath + @"\" + mapName + ".svc");
                if (fi.Exists) fi.Delete();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
                return false;
            }
        }

        static internal void mapDocument_MapAdded(IMap map)
        {
            try
            {
                Console.WriteLine("Map Added:" + map.Name);

                foreach (IDatasetElement element in map.MapElements)
                {
                    Console.WriteLine("     >> " + element.Title + " added");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }
        static internal void mapDocument_MapDeleted(IMap map)
        {
            try
            {
                Console.WriteLine("Map Removed: " + map.Name);
            }
            catch (Exception ex)
            {
                Logger.Log(loggingMethod.error, "LoadConfig: " + ex.Message);
            }
        }

        static internal int Port
        {
            get { return _port; }
            set
            {
                _port = value;
                MapServerConfig.ServerConfig serverConfig = MapServerConfig.ServerByInstancePort(_port);
                ServicesPath = gView.Framework.system.SystemVariables.MyCommonApplicationData + @"\mapServer\Services\" + serverConfig.Port;

                try
                {
                    MapServerConfig.ServerConfig.InstanceConfig InstanceConfig = MapServerConfig.InstanceByInstancePort(value);
                    if (serverConfig != null && InstanceConfig != null)
                    {
                        OutputPath = serverConfig.OutputPath.Trim();
                        OutputUrl = serverConfig.OutputUrl.Trim();
                        TileCachePath = serverConfig.TileCachePath.Trim();

                        Globals.MaxThreads = InstanceConfig.MaxThreads;
                        Globals.QueueLength = InstanceConfig.MaxQueueLength;

                        Logger.Log(loggingMethod.request, "Output Path: '" + OutputPath + "'");
                        Logger.Log(loggingMethod.request, "Output Url : '" + OutputUrl + "'");
                    }
                    ThreadQueue = new ThreadQueue<IServiceRequestContext>(Globals.MaxThreads, Globals.QueueLength);
                }
                catch (Exception ex)
                {
                    Logger.Log(loggingMethod.error, "IMS.Port(set): " + ex.Message + "\r\n" + ex.StackTrace);
                }
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = String.Empty;
            msg += "UnhandledException:\n";
            if (e.ExceptionObject is Exception)
            {
                msg += "Exception:" + ((Exception)e.ExceptionObject).Message + "\n";
                msg += "Stacktrace:" + ((Exception)e.ExceptionObject).StackTrace + "\n";
            }
            Logger.Log(loggingMethod.error, msg);
            System.Environment.Exit(1);
        }
    }
}