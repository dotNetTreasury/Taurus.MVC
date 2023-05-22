﻿using System;
using CYQ.Data.Tool;
using CYQ.Data.Table;
using System.Collections.Generic;
using Taurus.Mvc;
using CYQ.Data;
using System.IO;
using Taurus.MicroService;
using Taurus.Plugin.Limit;
using System.Threading;
using System.Diagnostics;
using Taurus.Plugin.Doc;
using System.Configuration;
using System.Net;
using System.Reflection;
using System.Xml;

namespace Taurus.Plugin.Admin
{

    /// <summary>
    /// Taurus Admin Management Center。
    /// </summary>
    internal partial class AdminController : Controller
    {
        protected override string HtmlFolderName
        {
            get
            {
                return AdminConfig.HtmlFolderName;
            }
        }
        /// <summary>
        /// 账号检测是否登陆状态
        /// </summary>
        /// <returns></returns>
        public override bool BeforeInvoke()
        {
            switch (MethodName)
            {
                case "login":
                    return true;
                default:
                    if (Context.Session["login"] == null)
                    {
                        //检测账号密码，跳转登陆页
                        Response.Redirect("login");
                        return false;
                    }
                    break;
            }
            return true;
        }
    }

    /// <summary>
    /// 首页：
    /// </summary>
    internal partial class AdminController
    {
        private MDictionary<string, List<HostInfo>> _HostList;
        private MDictionary<string, List<HostInfo>> HostList
        {
            get
            {
                if (_HostList == null)
                {
                    if (MsConfig.IsServer)
                    {
                        _HostList = Server.Gateway.HostList;
                    }
                    else if (MsConfig.IsClient)
                    {
                        _HostList = Client.Gateway.HostList;
                    }
                }
                return _HostList;
            }
        }

        private List<HostInfo> GetHostList(string name, bool withStar)
        {
            if (MsConfig.IsServer)
            {
                return Server.Gateway.GetHostList(name, withStar);
            }
            return Client.Gateway.GetHostList(name, withStar);
        }

        private string GetMsType()
        {

            if (MsConfig.IsRegCenterOfMaster)
            {
                return "Register Center of Master";
            }
            else if (MsConfig.IsRegCenter)
            {
                return "Register Center of Slave" + (Server.IsLiveOfMasterRC ? "" : " ( Master connection refused )");
            }
            else if (MsConfig.IsGateway)
            {
                return "Gateway" + (Server.IsLiveOfMasterRC ? "" : " ( Register center connection refused )"); ;
            }
            else if (MsConfig.IsClient)
            {
                return "Client of MicroService" + (Client.IsLiveOfMasterRC ? "" : " ( Register center connection refused )");
            }
            else
            {
                return "None";
            }

        }

        private string GetRouteMode()
        {
            switch (MvcConfig.RouteMode)
            {
                case 1:
                    return "1 【/controller/method】";
                case 2:
                    return "2 【/module/controller/method】";
            }
            return "0 【/method】 (code in DefaultController.cs)";
        }

        /// <summary>
        /// 微服务UI首页
        /// </summary>
        public void Index()
        {
            if (View != null)
            {
                View.KeyValue.Set("Version", MvcConst.Version);
                View.KeyValue.Set("MsType", GetMsType());
                //基础信息：
                if (MsConfig.IsServer)
                {
                    View.KeyValue.Set("MsKey", MsConfig.Server.RcKey);
                    View.KeyValue.Set("Path", MsConfig.Server.RcPath);
                }
                if (HostList != null && HostList.Count > 0)
                {
                    BindNamesView();
                    BindDefaultView();
                }
            }
        }
        private void BindNamesView()
        {
            MDataTable dtServer = new MDataTable();
            dtServer.Columns.Add("Name,Count");
            MDataTable dtDomain = dtServer.Clone();
            MDataTable dtClient = dtServer.Clone();

            var hostList = HostList;//先获取引用【避免执行过程，因线程更换了引用的对象】
            foreach (var item in hostList)
            {
                if (item.Key == "RegCenter" || item.Key == "RegCenterOfSlave" || item.Key == "Gateway")
                {
                    dtServer.NewRow(true).Sets(0, item.Key, item.Value.Count);
                }
                else if (item.Key.Contains("."))
                {
                    dtDomain.NewRow(true).Sets(0, item.Key, item.Value.Count);
                }
                else
                {
                    dtClient.NewRow(true).Sets(0, item.Key, item.Value.Count);
                }

            }
            dtServer.Bind(View, "serverNamesView");
            dtDomain.Bind(View, "domainNamesView");
            dtClient.Bind(View, "clientNamesView");

        }
        private void BindDefaultView()
        {
            string host = Query<string>("h");
            string name = Query<string>("n", "RegCenter");
            if (!string.IsNullOrEmpty(host) || (name != "RegCenter" && name != "RegCenterOfSlave" && name != "Gateway"))
            {
                var hostList = HostList;
                if (hostList.ContainsKey("Gateway"))
                {
                    View.KeyValue.Set("GatewayUrl", hostList["Gateway"][0].Host);
                }
                else if (hostList.ContainsKey("RegCenter"))
                {
                    View.KeyValue.Set("GatewayUrl", hostList["RegCenter"][0].Host);
                }
            }
            if (!string.IsNullOrEmpty(host))
            {
                BindViewByHost(host);
            }
            else
            {
                BindViewByName(name);
            }

        }
        private void BindViewByName(string name)
        {
            List<HostInfo> list = GetHostList(name, false);
            if (list.Count > 0)
            {
                MDataTable dt = MDataTable.CreateFrom(list);
                if (dt != null)
                {
                    dt.Columns.Add("Name");
                    dt.Columns["Name"].Set(name);
                    if (MsConfig.IsServer)
                    {
                        dt.Columns.Add("RemoteExit");
                        dt.Columns["RemoteExit"].Set("Stop");
                    }
                    View.OnForeach += View_OnForeach;
                    dt.Bind(View);
                }
            }
        }
        private void BindViewByHost(string host)
        {
            MDataTable dt = null;
            var hostList = HostList;
            foreach (var item in hostList)
            {
                foreach (var info in item.Value)
                {
                    if (info.Host == host)
                    {
                        if (dt == null)
                        {
                            dt = MDataTable.CreateFrom(info);
                            dt.Columns.Add("Name");
                            dt.Rows[0].Set("Name", item.Key);
                        }
                        else
                        {
                            dt.NewRow(true).Set("Name", item.Key).LoadFrom(info);
                        }
                        break;
                    }
                }
            }
            if (dt != null)
            {
                if (MsConfig.IsServer)
                {
                    dt.Columns.Add("RemoteExit");
                    dt.Columns["RemoteExit"].Set("Stop");
                }
                View.OnForeach += View_OnForeach;
                dt.Bind(View);
            }

        }
        private string View_OnForeach(string text, MDictionary<string, string> values, int rowIndex)
        {
            DateTime dt;
            if (DateTime.TryParse(values["RegTime"], out dt))
            {
                string time = dt.ToString("yyyy-MM-dd HH:mm:ss");
                values["RegTime"] = time;
            }
            return text;
        }
    }

    /// <summary>
    /// 登陆退出
    /// </summary>
    internal partial class AdminController
    {
        public void Logout()
        {
            Context.Session["login"] = null;
            Response.Redirect("login");
        }
        public void Login()
        {
            if (Context.Session["login"] != null)
            {
                Response.Redirect("index");
            }
        }
        public void BtnLogin()
        {
            string uid = Query<string>("uid");
            string pwd = Query<string>("pwd");
            bool isOK = uid == AdminConfig.UserName && pwd == AdminConfig.Password;
            if (!isOK)
            {
                string[] items = IO.Read(AdminConst.AccountPath).Split(',');
                isOK = items.Length == 2 && items[0] == uid && items[1] == pwd;
            }
            if (isOK)
            {
                Context.Session["login"] = "1";
                Response.Redirect("index");
                return;
            }
            View.Set("msg", "user or password is error.");
        }
    }
    /// <summary>
    /// 日志、配置信息
    /// </summary>
    internal partial class AdminController
    {
        /// <summary>
        /// 错误日志
        /// </summary>
        public void Log()
        {
            string logPath = AppConfig.WebRootPath + AppConfig.Log.Path;
            if (Directory.Exists(logPath))
            {
                string[] files = Directory.GetFiles(logPath, "*.txt", SearchOption.TopDirectoryOnly);
                MDataTable dt = new MDataTable();
                dt.Columns.Add("FileName");
                foreach (string file in files)
                {
                    dt.NewRow(true).Set(0, Path.GetFileName(file));
                }
                dt.Rows.Sort("FileName desc");
                dt.Bind(View, "fileList");

            }
        }
        public void LogDetail()
        {
            string fileName = Query<string>("filename");
            if (!string.IsNullOrEmpty(fileName))
            {
                string logPath = AppConfig.WebRootPath + AppConfig.Log.Path;
                string[] files = Directory.GetFiles(logPath, fileName, SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0)
                {
                    string logDetail = IOHelper.ReadAllText(files[0]);
                    View.KeyValue.Set("detail", System.Web.HttpUtility.HtmlEncode(logDetail).Replace("\n", "<br/>"));
                }
            }
        }

        /// <summary>
        /// AppSetting 基础配置信息
        /// </summary>
        public void Config()
        {
            View.KeyValue.Set("Version", MvcConst.Version);
            string type = Query<string>("t", "mvc").ToLower();

            MDataTable dt = new MDataTable();
            dt.Columns.Add("ConfigKey,ConfigValue,Description");
            if (type == "mvc")
            {
                #region Mvc

                dt.NewRow(true).Sets(0, "Taurus.RunUrl", MvcConfig.RunUrl, "Application run url.");
                dt.NewRow(true).Sets(0, "Taurus.DefaultUrl", MvcConfig.DefaultUrl, "Application default url.");
                dt.NewRow(true).Sets(0, "Taurus.IsAllowCORS", MvcConfig.IsAllowCORS, "Application is allow cross-origin resource sharing.");

                dt.NewRow(true).Sets(0, "Taurus.RouteMode", GetRouteMode(), "Route mode for selected.");
                dt.NewRow(true).Sets(0, "Taurus.Controllers", MvcConfig.Controllers, "Load controller names.");
                dt.NewRow(true).Sets(0, "Taurus.Views", MvcConfig.Views, "Mvc view folder name.");
                dt.NewRow(true).Sets(0, "Taurus.SslPath", MvcConfig.SslPath, "Ssl path for https (*.pfx for ssl , *.txt for pwd).");
                dt.NewRow(true).Sets(0, "----------SslCertificate - Count", MvcConfig.SslCertificate.Count, "Num of ssl for https (Show Only).");
                if (MvcConfig.SslCertificate.Count > 0)
                {
                    int i = 1;
                    foreach (string name in MvcConfig.SslCertificate.Keys)
                    {
                        dt.NewRow(true).Sets(0, "----------SslCertificate - " + i, name, "Domain ssl for https (Show Only).");
                        i++;
                    }
                }
                dt.NewRow(true).Sets(0, "Taurus.Suffix", MvcConfig.Suffix, "Deal with mvc suffix.");
                dt.NewRow(true).Sets(0, "Taurus.SubAppName", MvcConfig.SubAppName, "Name of deploy as sub application.");
                #endregion
            }
            else if (type == "plugin")
            {
                #region Plugin

                dt.NewRow(true).Sets(0, "Admin.IsEnable", AdminConfig.IsEnable, "Admin plugin is enable.");
                dt.NewRow(true).Sets(0, "Admin.Path", "/" + AdminConfig.Path, "Admin url path.");
                dt.NewRow(true).Sets(0, "Admin.HtmlFolderName", AdminConfig.HtmlFolderName, "Mvc view folder name for admin.");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Admin.UserName", AdminConfig.UserName, "Admin account.");
                dt.NewRow(true).Sets(0, "Admin.Password", string.IsNullOrEmpty(AdminConfig.Password) ? "" : "******", "Admin password.");

                string[] items = IO.Read(AdminConst.AccountPath).Split(',');
                if (items.Length == 2)
                {
                    dt.NewRow(true).Sets(0, "Admin.UserName - Setting ", items[0], "Admin account by setting.");
                    dt.NewRow(true).Sets(0, "Admin.Password - Setting", "******", "Admin password by setting.");
                }

                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Doc.IsEnable", DocConfig.IsEnable, "Doc plugin is enable.");
                dt.NewRow(true).Sets(0, "Doc.Path", "/" + DocConfig.Path, "Doc url path.");
                dt.NewRow(true).Sets(0, "Doc.HtmlFolderName", DocConfig.HtmlFolderName, "Mvc view folder name for doc.");
                dt.NewRow(true).Sets(0, "Doc.DefaultImg", DocConfig.DefaultImg, "Default images path for doc auto test,as :/App_Data/xxx.jpg");
                dt.NewRow(true).Sets(0, "Doc.DefaultParas", DocConfig.DefaultParas, "global para for doc auto test,as :ack,token");

                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Limit.IP.IsEnable", LimitConfig.IP.IsEnable, "IP limit plugin is enable.");
                dt.NewRow(true).Sets(0, "Limit.IP.IsSync", LimitConfig.IP.IsSync, "IP limit : is sync ip blackname list from register center.");

                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Limit.Ack.IsEnable", LimitConfig.Ack.IsEnable, "Ack limit plugin is enable.");
                dt.NewRow(true).Sets(0, "Limit.Ack.Key", LimitConfig.Ack.Key, "Ack limit : secret key.");
                dt.NewRow(true).Sets(0, "Limit.Ack.IsVerifyDecode", LimitConfig.Ack.IsVerifyDecode, "Ack limit : ack must be decode and valid.");
                dt.NewRow(true).Sets(0, "Limit.Ack.IsVerifyUsed", LimitConfig.Ack.IsVerifyUsed, "Ack limit : ack use once only.");

                #endregion
            }
            else if (type == "microservice")
            {
                #region MicroService

                dt.NewRow(true).Sets(0, "MicroServer Type", GetMsType(), "Type of current microservice (Show Only).");
                if (MsConfig.IsServer)
                {
                    dt.NewRow(true).Sets(0, "MicroServer.Server.Name", MsConfig.Server.Name, "Server name.");
                    dt.NewRow(true).Sets(0, "MicroServer.Server.RcKey", MsConfig.Server.RcKey, "Register center secret key.");
                    dt.NewRow(true).Sets(0, "MicroServer.Server.RcUrl", MsConfig.Server.RcUrl, "Register center url.");
                    dt.NewRow(true).Sets(0, "MicroServer.Server.RcPath", "/" + MsConfig.Server.RcPath, "Register center local path.");
                    dt.NewRow(true).Sets(0, "MicroServer.Server.GatewayTimeout", MsConfig.Server.GatewayTimeout + " (s)", "Gateway timeout (second) for request forward.");
                    dt.NewRow(true).Sets(0, "MicroServer Gateway Proxy LastTime", Rpc.Gateway.LastProxyTime.ToString("yyyy-MM-dd HH:mm:ss"), "The last time the proxy forwarded the request (Show Only).");
                }

                if (MsConfig.IsClient)
                {
                    if (MsConfig.IsServer)
                    {
                        dt.NewRow(true);
                    }
                    dt.NewRow(true).Sets(0, "MicroServer.Client.Name", MsConfig.Client.Name, "Client module name.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.Domain", MsConfig.Client.Domain, "Client bind domain.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.Version", MsConfig.Client.Version, "Client web version.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.RemoteExit", MsConfig.Client.RemoteExit, "Client is allow remote stop by register center.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.RcKey", MsConfig.Client.RcKey, "Register center secret key.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.RcUrl", MsConfig.Client.RcUrl, "Register center url.");
                    dt.NewRow(true).Sets(0, "MicroServer.Client.RcPath", "/" + MsConfig.Client.RcPath, "Register center local path.");

                }
                #endregion
            }
            else if (type == "cyq.data")
            {
                dt.NewRow(true).Sets(0, "AutoCache.IsEnable", AppConfig.AutoCache.IsEnable, "Use auto cache.");
                dt.NewRow(true).Sets(0, "Debug.IsEnable", AppConfig.Debug.IsEnable, "Record sql when dev debug.");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Log.IsEnable", AppConfig.Log.IsEnable, "Write log to file or database on error,otherwise throw exception.");
                dt.NewRow(true).Sets(0, "Log.Path", AppConfig.Log.Path, "Log folder name or path.");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "DB.SchemaMapPath", AppConfig.DB.SchemaMapPath, "Database metadata cache path.");
                dt.NewRow(true).Sets(0, "DB.CommandTimeout", AppConfig.DB.CommandTimeout + " (s)", "Timeout for database command.");
                dt.NewRow(true).Sets(0, "DB.SqlFilter", AppConfig.DB.SqlFilter + " (ms)", "Write sql to log file when sql exe time > value(value must>0).");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "Aop", AppConfig.Aop, "Aop config :【Aop-Class-FullName,DllName】");
                dt.NewRow(true).Sets(0, "EncryptKey", AppConfig.EncryptKey, "Encrypt key for EncryptHelper tool.");
                dt.NewRow(true).Sets(0, "DefaultCacheTime", AppConfig.DefaultCacheTime + " (m)", "Default cache time (minute).");
            }
            else if (type == "log")
            {
                dt.NewRow(true).Sets(0, "Log.IsEnable", AppConfig.Log.IsEnable, "Write log to file or database on error,otherwise throw exception.");
                dt.NewRow(true).Sets(0, "Log.Path", AppConfig.Log.Path, "Log folder name or path.");
                dt.NewRow(true).Sets(0, "Log.TableName", AppConfig.Log.TableName, "Log tablename on log database.");
                dt.NewRow(true).Sets(0, "LogConn", HideConnPassword(AppConfig.Log.Conn), "Log database connection string.");
            }
            else if (type == "conn")
            {
                foreach (ConnectionStringSettings item in ConfigurationManager.ConnectionStrings)
                {
                    dt.NewRow(true).Sets(0, item.Name, HideConnPassword(item.ConnectionString), "DataBaseType : " + DBTool.GetDataBaseType(item.ConnectionString));
                }
            }
            else if (type == "autocache")
            {
                dt.NewRow(true).Sets(0, "AutoCache.IsEnable", AppConfig.AutoCache.IsEnable, "AutoCache is enabled.");
                dt.NewRow(true).Sets(0, "AutoCache.Tables", AppConfig.AutoCache.Tables, "Set the tables that need to be cached by specifying their names separated by commas.");
                dt.NewRow(true).Sets(0, "AutoCache.IngoreTables", AppConfig.AutoCache.IngoreTables, "Set the tables that no need to be cached by specifying their names separated by commas.");
                dt.NewRow(true).Sets(0, "AutoCache.IngoreColumns", AppConfig.AutoCache.IngoreColumns, "Set column names that will not be affected by updates, using a JSON format as {tablename:'col1,col2'}.");
                dt.NewRow(true).Sets(0, "AutoCache.TaskTime", AppConfig.AutoCache.TaskTime + " (ms)", "When AutoCacheConn is enabled, the task time (in milliseconds) for regularly scanning the database.");
                dt.NewRow(true).Sets(0, "AutoCacheConn", HideConnPassword(AppConfig.AutoCache.Conn), "For auto remove cache when database data change.");
            }

            else if (type == "redis")
            {
                if (!string.IsNullOrEmpty(AppConfig.Redis.Servers))
                {
                    dt.NewRow(true);
                    dt.NewRow(true).Sets(0, "RedisUseDBCount", AppConfig.Redis.UseDBCount, "Redis use db count.");
                    dt.NewRow(true).Sets(0, "RedisUseDBIndex", AppConfig.Redis.UseDBIndex, "Redis use db index.");
                    string[] items = AppConfig.Redis.Servers.Split(',');
                    dt.NewRow(true).Sets(0, "----------RedisServers - Count", items.Length, "Num of server node for redis (Show Only).");

                    for (int i = 0; i < items.Length; i++)
                    {
                        dt.NewRow(true).Sets(0, "----------RedisServers - " + (i + 1), items[i], "Server node for redis (Show Only).");
                    }

                    if (!string.IsNullOrEmpty(AppConfig.Redis.ServersBak))
                    {
                        items = AppConfig.Redis.ServersBak.Split(',');
                        dt.NewRow(true).Sets(0, "----------RedisServersBak - Count", items.Length, "Num of server node for redis bak(Show Only).");
                        for (int i = 0; i < items.Length; i++)
                        {
                            dt.NewRow(true).Sets(0, "----------RedisServersBak - " + (i + 1), items[i], "Server node for redis (Show Only).");
                        }
                    }
                }
            }
            else if (type == "memcache")
            {
                if (!string.IsNullOrEmpty(AppConfig.MemCache.Servers))
                {
                    string[] items = AppConfig.MemCache.Servers.Split(',');
                    dt.NewRow(true).Sets(0, "----------MemCacheServers - Count", items.Length, "Num of server node for memcache (Show Only).");

                    for (int i = 0; i < items.Length; i++)
                    {
                        dt.NewRow(true).Sets(0, "----------MemCacheServers - " + (i + 1), items[i], "Server node for memcache (Show Only).");
                    }

                    if (!string.IsNullOrEmpty(AppConfig.MemCache.ServersBak))
                    {
                        items = AppConfig.MemCache.ServersBak.Split(',');
                        dt.NewRow(true).Sets(0, "----------MemCacheServersBak - Count", items.Length, "Num of server node for memcache bak(Show Only).");
                        for (int i = 0; i < items.Length; i++)
                        {
                            dt.NewRow(true).Sets(0, "----------MemCacheServersBak - " + (i + 1), items[i], "Server node for memcache (Show Only).");
                        }
                    }
                }
            }

            else if (type == "debug")
            {
                dt.NewRow(true).Sets(0, "Debug.IsEnable", AppConfig.Debug.IsEnable, "Record sql when dev debug.");
            }
            else if (type == "database")
            {
                dt.NewRow(true).Sets(0, "DB.CommandTimeout", AppConfig.DB.CommandTimeout + " (s)", "Timeout for database command.");
                dt.NewRow(true).Sets(0, "DB.SchemaMapPath", AppConfig.DB.SchemaMapPath, "Database metadata cache path.");
                dt.NewRow(true).Sets(0, "DB.SqlFilter", AppConfig.DB.SqlFilter + " (ms)", "Write sql to log file when sql exe time > value(value must>0).");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "DB.HiddenFields", AppConfig.DB.HiddenFields, "Hide fields that are not returned when querying.");
                dt.NewRow(true).Sets(0, "DB.DeleteField", AppConfig.DB.DeleteField, "Soft-deletion field name (if a table has this specified field name, MAction's delete operation will be changed to an update operation).");
                dt.NewRow(true).Sets(0, "DB.EditTimeFields", AppConfig.DB.EditTimeFields, "Name of the update time field (if the specified field name exists in the table, the update time will be automatically updated).");
                dt.NewRow(true);

                dt.NewRow(true).Sets(0, "DB.IsPostgreLower", AppConfig.DB.IsPostgreLower, "Postgres is in lowercase mode.");
                dt.NewRow(true).Sets(0, "DB.IsTxtReadOnly", AppConfig.DB.IsTxtReadOnly, "Txt database is read-only (used for demo purposes to prevent demo accounts or data from being deleted).");
                dt.NewRow(true);
                dt.NewRow(true).Sets(0, "DB.AutoID", AppConfig.DB.AutoID, "The sequence id config for oracle.");
                dt.NewRow(true).Sets(0, "DB.EntitySuffix", AppConfig.DB.EntitySuffix, "Entity suffix which will be ignore when orm operate.");
                dt.NewRow(true).Sets(0, "DB.MasterSlaveTime", AppConfig.DB.MasterSlaveTime + " (s)", "The duration of user operations on the primary database when using read-write separation.");
            }
            dt.Bind(View);
        }
        private string HideConnPassword(string conn)
        {
            if (!string.IsNullOrEmpty(conn))
            {
                int i = conn.IndexOf("pwd=");
                if (i > 0)
                {
                    int end = conn.IndexOf(";", i);
                    if (end > 0)
                    {
                        return conn.Substring(0, i + 4) + "******" + conn.Substring(end);
                    }
                    else
                    {
                        return conn.Substring(0, i + 4) + "******";
                    }
                }
            }

            return conn;
        }
        /// <summary>
        /// 操作系统环境信息
        /// </summary>
        public void OSInfo()
        {
            MDataTable dtTaurus = new MDataTable();
            dtTaurus.Columns.Add("ConfigKey,ConfigValue,Description");
            string type = Query<string>("t", "os").ToLower();

            if (type == "os")
            {
                dtTaurus.NewRow(true).Sets(0, "Client-IP", Request.UserHostAddress, "Client ip.");
                dtTaurus.NewRow(true).Sets(0, "Server-IP", MvcConst.HostIP, "Server ip.");
                dtTaurus.NewRow(true).Sets(0, "Taurus-Version", "V" + MvcConst.Version, "Version of the Taurus.Core.dll.");
                dtTaurus.NewRow(true).Sets(0, "Orm-Version", "V" + AppConfig.Version, "Version of the CYQ.Data.dll.");
                dtTaurus.NewRow(true).Sets(0, "AppPath", AppConfig.RunPath, "Web application path of the working directory.");
                dtTaurus.NewRow(true);
                dtTaurus.NewRow(true).Sets(0, "Runtime-Version", (AppConfig.IsNetCore ? ".Net Core - " : ".Net Framework - ") + Environment.Version, "Version of the common language runtime.");
                dtTaurus.NewRow(true).Sets(0, "OS-Version", Environment.OSVersion, "Operating system.");
                dtTaurus.NewRow(true).Sets(0, "ProcessID", MvcConst.ProcessID, "Process id.");
                dtTaurus.NewRow(true).Sets(0, "ThreadID", Thread.CurrentThread.ManagedThreadId, "Identifier for the managed thread.");
                dtTaurus.NewRow(true).Sets(0, "ThreadCount", Process.GetCurrentProcess().Threads.Count, "Number of threads for the process.");
                long tc = Environment.TickCount > 0 ? (long)Environment.TickCount : ((long)int.MaxValue + (Environment.TickCount & int.MaxValue));
                TimeSpan ts = TimeSpan.FromMilliseconds(tc);
                dtTaurus.NewRow(true).Sets(0, "TickCount", (int)ts.TotalSeconds + "s | " + (int)ts.TotalMinutes + "m | " + (int)ts.TotalHours + "h | " + (int)ts.TotalDays + "d", "Time since the system started(max(days)<=49.8)).");
                dtTaurus.NewRow(true).Sets(0, "ProcessorCount", Environment.ProcessorCount, "Number of processors on the machine.");
                dtTaurus.NewRow(true).Sets(0, "MachineName", Environment.MachineName, "Name of computer.");
                dtTaurus.NewRow(true).Sets(0, "UserName", Environment.UserName, "Name of the person who is logged on to Windows.");
                dtTaurus.NewRow(true).Sets(0, "WorkingSet", Environment.WorkingSet / 1024 + "KB | " + Environment.WorkingSet / 1024 / 1024 + "MB", "Physical memory mapped to the process context.");
                dtTaurus.NewRow(true).Sets(0, "CurrentDirectory", Environment.CurrentDirectory, "Fully qualified path of the working directory.");
            }
            else if (type == "ass")
            {
                Assembly[] assList = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assList)
                {
                    string desc = assembly.FullName;
                    object[] attrs = assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                    if (attrs != null && attrs.Length > 0)
                    {
                        desc = ((AssemblyDescriptionAttribute)attrs[0]).Description;
                    }
                    else
                    {
                        attrs = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                        if (attrs != null && attrs.Length > 0)
                        {
                            desc = ((AssemblyTitleAttribute)attrs[0]).Title;
                        }
                    }
                    AssemblyName assName = assembly.GetName();
                    dtTaurus.NewRow(true).Sets(0, assName.Name, assName.Version.ToString(), desc);
                }
                dtTaurus.Rows.Sort("ConfigKey");
            }
            dtTaurus.Bind(View);
        }
    }

    /// <summary>
    /// 设置：账号、IP黑名单、手工添加微服务客户端
    /// </summary>
    internal partial class AdminController
    {
        #region 页面呈现

        public void Setting() { }

        private MDataTable menuTable;
        public void Menu()
        {
            string menuList = IO.Read(AdminConst.MenuAddPath);
            if (!string.IsNullOrEmpty(menuList))
            {
                menuTable = new MDataTable();
                menuTable.Columns.Add("MenuName,HostName,HostUrl");
                MDataTable dt = new MDataTable();
                dt.Columns.Add("MenuName");
                List<string> menus = new List<string>();
                string[] items = menuList.Split('\n');
                foreach (string item in items)
                {
                    string[] names = item.Split(',');
                    if (names.Length > 2)
                    {
                        menuTable.NewRow(true).Sets(0, names[0].Trim(), names[1].Trim(), names[2].Trim());
                    }
                    string name = names[0].Trim();
                    if (!menus.Contains(name.ToLower()))
                    {
                        menus.Add(name.ToLower());
                        dt.NewRow(true).Set(0, name);
                    }
                }
                View.OnForeach += View_OnForeach_Menu;
                dt.Bind(View, "menuList");
            }
        }

        private string View_OnForeach_Menu(string text, MDictionary<string, string> values, int rowIndex)
        {
            string menu = values["MenuName"];
            if (!string.IsNullOrEmpty(menu))
            {
                //循环嵌套：1-获取子数据
                MDataTable dt = menuTable.FindAll("MenuName='" + menu + "'");
                if (dt != null && dt.Rows.Count > 0)
                {
                    //循环嵌套：2 - 转为节点
                    XmlNode xmlNode = View.CreateNode("div", text);
                    //循环嵌套：3 - 获取子节点，以便进行循环
                    XmlNode hostNode = View.GetByID("hostList", xmlNode);
                    if (hostNode != null)
                    {
                        //循环嵌套：4 - 子节点，循环绑定数据。
                        View.SetForeach(dt, hostNode, hostNode.InnerXml, null);
                        //循环嵌套：5 - 返回整个节点的内容。
                        return xmlNode.InnerXml;
                    }
                }
            }

            return text;
        }


        public void SettingOfAccount()
        {
            if (!IsHttpPost)
            {
                View.KeyValue.Set("UserName", AdminConfig.UserName);
            }
        }


        public void SettingOfHostAdd()
        {
            if (!IsHttpPost)
            {
                if (MsConfig.IsRegCenterOfMaster)
                {
                    string hostList = IO.Read(AdminConst.HostAddPath);
                    View.KeyValue.Add("HostList", hostList);
                }
                else
                {
                    View.Set("btnAddHost", CYQ.Data.Xml.SetType.Disabled, "true");
                    View.Set("hostList", CYQ.Data.Xml.SetType.Disabled, "true");
                }
            }
        }

        public void SettingOfMenuAdd()
        {
            if (!IsHttpPost)
            {
                string menuList = IO.Read(AdminConst.MenuAddPath);
                View.KeyValue.Add("MenuList", menuList);
            }
        }
        #endregion

        #region 页面点击事件

        /// <summary>
        /// 添加管理员2账号
        /// </summary>
        public void BtnSaveAccount()
        {
            string uid = Query<string>("uid");
            string pwd = Query<string>("pwd");
            string pwdAgain = Query<string>("pwdAgain");
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(pwd))
            {
                View.Set("msg", "User or passowrd can't be empty.");
            }
            else if (pwd != pwdAgain)
            {
                View.Set("msg", "Password must be same.");
            }
            if (uid.Contains(",") || pwd.Contains(","))
            {
                View.Set("msg", "User or password can't contain ','.");
            }
            else
            {
                IO.Write(AdminConst.AccountPath, uid + "," + pwd);
                View.Set("msg", "Save success.");
            }

        }
        /// <summary>
        /// 添加注册主机
        /// </summary>
        public void BtnAddHost()
        {
            if (!MsConfig.IsRegCenterOfMaster)
            {
                View.Set("msg", "Setting only for register center of master.");
                return;
            }

            string hostList = Query<string>("hostList");
            Server.RegCenter.AddHostByAdmin(hostList);
            IO.Write(AdminConst.HostAddPath, hostList);
            View.KeyValue.Add("HostList", hostList);
            View.Set("msg", "Save success.");
        }
        /// <summary>
        /// 添加IP黑名单
        /// </summary>
        public void SettingOfIpBlackname()
        {
            if (!IsHttpPost)
            {
                string ipList = IO.Read(AdminConst.IPBlacknamePath);
                View.KeyValue.Add("IPList", ipList);
            }
        }
        /// <summary>
        /// 添加黑名单
        /// </summary>
        public void BtnAddIPBlackname()
        {
            string ipList = Query<string>("ipList");
            IPLimit.ResetIPList(ipList);
            LimitConfig.IP.IsSync = false;//手工保存后，重启服务前不再与注册同心保持同步。
            View.KeyValue.Add("IPList", ipList);
            View.Set("msg", "Save success.");
        }

        /// <summary>
        /// 添加自定义菜单
        /// </summary>
        public void BtnAddMenu()
        {
            string menuList = Query<string>("menuList");
            IO.Write(AdminConst.MenuAddPath, menuList);
            View.KeyValue.Add("MenuList", menuList);
            View.Set("msg", "Save success.");
        }
        #endregion
    }
}
