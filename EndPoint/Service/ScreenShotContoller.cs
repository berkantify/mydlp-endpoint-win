﻿//    Copyright (C) 2012 Huseyin Ozgur Batur <ozgur@medra.com.tr>
//
//--------------------------------------------------------------------------
//    This file is part of MyDLP.
//
//    MyDLP is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    MyDLP is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with MyDLP.  If not, see <http://www.gnu.org/licenses/>.
//--------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyDLP.EndPoint.Core;
using System.Management;
using System.Collections;
using System.Diagnostics;

namespace MyDLP.EndPoint.Service
{
    class ScreenShotContoller
    {
        static ManagementEventWatcher startWatch;
        static ManagementEventWatcher stopWatch;

        static ArrayList sensitiveProcessesList;
        static ArrayList activeSensitiveProcesses;

        static bool block;

        static readonly string WMI_START_QUERY =
            @"SELECT * FROM Win32_ProcessStartTrace";

        static readonly string WMI_STOP_QUERY =
            @"SELECT * FROM Win32_ProcessStopTrace";



        static public void Start()
        {            
            try
            {
                Logger.GetInstance().Debug("ScreenShotContoller Start");

                sensitiveProcessesList = new ArrayList();
                activeSensitiveProcesses = new ArrayList();

                foreach (String pName in Configuration.ScreentShotProcesses.Split(','))
                {
                    String processName = pName.Trim().ToLower();
                    if (processName.Length > 0)
                    {
                        sensitiveProcessesList.Add(processName);
                    }
                }

                if (sensitiveProcessesList.Count == 0)
                    return;

                //remove device for improper shutdown
                if (Core.SvcController.IsServiceInstalled("MyDLPKBF"))
                {
                    MyDLPEP.KbFilterController.GetInstance().DeactivateDevice();
                    MyDLPEP.KbFilterController.GetInstance().Stop();
                }
                Logger.GetInstance().Debug("mydlpepwin tries to install mydlpkbf");
                MyDLPEP.KbFilterController.GetInstance().Start();
                                           

                startWatch = new ManagementEventWatcher(
                    new WqlEventQuery(WMI_START_QUERY));
                startWatch.EventArrived
                    += new EventArrivedEventHandler(startWatch_EventArrived);
                startWatch.Start();


                stopWatch = new ManagementEventWatcher(
                    new WqlEventQuery(WMI_STOP_QUERY));
                stopWatch.EventArrived
                    += new EventArrivedEventHandler(stopWatch_EventArrived);
                stopWatch.Start();

                foreach (Process process in Process.GetProcesses())
                {
                    String pName = process.ProcessName.ToLower() + ".exe";
                    if (sensitiveProcessesList.Contains(pName))
                    {
                        if (!activeSensitiveProcesses.Contains(pName))
                        {
                            activeSensitiveProcesses.Add(pName);
                        }
                    }
                }

                if (activeSensitiveProcesses.Count != 0) Block();
                                            
            }
            catch (Exception e)
            {
                Logger.GetInstance().Error("ScreenShotContoller Start error: " + e.Message + " " + e.StackTrace);
            }
        }

        static public void Stop()
        {
            try
            {
                Logger.GetInstance().Debug("ScreenShotContoller Stop");
                startWatch.Stop();
                stopWatch.Stop();
               
                UnBlock();
                MyDLPEP.KbFilterController.GetInstance().Stop();              
            }
            catch (Exception e)
            {
                Logger.GetInstance().Error("ScreenShotContoller Stop error: " + e.Message + " " + e.StackTrace);
            }
        }

        static void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            String pName = ((String)e.NewEvent.Properties["ProcessName"].Value).ToLower();
            Logger.GetInstance().Debug(pName + " started");

            if (sensitiveProcessesList.Contains(pName))
            {
                if (!activeSensitiveProcesses.Contains(pName))
                {
                    activeSensitiveProcesses.Add(pName);
                }

                Block();
            }

        }

        static void stopWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            String pName = ((String)e.NewEvent.Properties["ProcessName"].Value).ToLower();
            Logger.GetInstance().Debug(pName + " stopped");

            if (activeSensitiveProcesses.Contains(pName))
            {
                Array a = Process.GetProcessesByName(pName.Substring(0, pName.Length - 4));
                if (a.Length == 0)
                    activeSensitiveProcesses.Remove(pName);

                if (activeSensitiveProcesses.Count == 0)
                {
                    UnBlock();
                }
            }
        }

        static void Block()
        {
            if (block)
                return;
            
            Logger.GetInstance().Debug("ScreenShotController block");

            MyDLPEP.KbFilterController.GetInstance().ActivateDevice();
            block = true;
        }

        static void UnBlock()
        {
            if (!block)
                return;

            Logger.GetInstance().Debug("ScreenShotController unblock");

            MyDLPEP.KbFilterController.GetInstance().DeactivateDevice();
            block = false;
        }
    }
}
