﻿using Capgemini.Xrm.Deployment.Config;
using Capgemini.Xrm.Deployment.Repository;
using Capgemini.Xrm.Deployment.SolutionImport.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Capgemini.Xrm.Deployment.SolutionImport
{
    public class PackageDeployer
    {
        #region Private Fields

        private readonly List<SolutionPackage> _packages = new List<SolutionPackage>();
        private readonly ICrmImportRepository _importRepo;
        private readonly int _sleepIntervalMiliseconds = 1000;
        private readonly int _asyncTimeoutSeconds = 1200;
        private readonly bool _importAsync = true;
        private readonly PackageDeployerConfigReader _configReader;

        #endregion Private Fields

        #region Constructors

        public PackageDeployer(ICrmImportRepository importRepo, PackageDeployerConfigReader configReader)
        {
            _importRepo = importRepo;
            _configReader = configReader;
            ReadConfiguration();
        }

        public PackageDeployer(ICrmImportRepository importRepo, int sleepIntervalMiliseconds, int asyncTimeoutSeconds, bool importAsync, PackageDeployerConfigReader configReader)
            : this(importRepo, configReader)
        {
            _sleepIntervalMiliseconds = sleepIntervalMiliseconds;
            _asyncTimeoutSeconds = asyncTimeoutSeconds;
            _importAsync = importAsync;
        }

        #endregion Constructors

        #region Public Events

        public event EventHandler<ImportUpdateEventArgs> RaiseImportUpdateEvent;

        protected virtual void OnRaiseImportUpdatEvent(ImportUpdateEventArgs e)
        {
            EventHandler<ImportUpdateEventArgs> handler = RaiseImportUpdateEvent;

            if (handler != null)
            {
                e.EventTime = DateTime.Now;
                handler(this, e);
            }
        }

        #endregion Public Events

        #region Public Methods and Properties

        public List<SolutionImporter> GetSolutionDetails
        {
            get
            {
                return _packages.Select(p => p.SolutionImporter).ToList();
            }
        }

        public void InstallHoldingSolutions()
        {
            if (_configReader.DontUseHoldingSulutions)
                return;

            foreach (var item in _packages)
            {
                if (!item.ImportSetting.DeleteOnly)
                {
                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = String.Format("Holding Solution installation started, PublishWorkflows:{0}, OverwriteUnmanagedCustomizations {1}", item.ImportSetting.PublishWorkflows, item.ImportSetting.OverwriteUnmanagedCustomizations)
                    });

                    var result = item.SolutionImporter.ImportHoldingSolution(_importAsync, true, _sleepIntervalMiliseconds, _asyncTimeoutSeconds, item.ImportSetting.PublishWorkflows, item.ImportSetting.OverwriteUnmanagedCustomizations);
                    //var result = item.SolutionImporter.ImportHoldingSolution(_importAsync, true, _sleepIntervalMiliseconds, _asyncTimeoutSeconds, true, false);

                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = string.Format("Holding Solution installation finished, status:{0}", result.ImportState)
                    });
                }
                else
                {
                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = "Holding Solution is not required"
                    });
                }
            }
        }

        public void DeleteOriginalSolutions()
        {
            var procList = _packages.ToList();
            procList.Reverse();

            foreach (var item in procList)
            {
                if (!_configReader.DontUseHoldingSulutions || item.ImportSetting.DeleteOnly)
                {
                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = "Original Solution deletion started"
                    });

                    var result = item.SolutionImporter.DeleteOriginalSolution(item.ImportSetting.DeleteOnly);

                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = result
                    });
                }
            }
        }

        public void InstallNewSolutions()
        {
            foreach (var item in _packages)
            {
                OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                {
                    SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                    Message = String.Format("Updated Solution installation started, PublishWorkflows:{0}, OverwriteUnmanagedCustomizations {1}", item.ImportSetting.PublishWorkflows, item.ImportSetting.OverwriteUnmanagedCustomizations)
                });

                var result = item.SolutionImporter.ImportUpdatedSolution(_importAsync, true, _sleepIntervalMiliseconds, _asyncTimeoutSeconds, item.ImportSetting.PublishWorkflows, item.ImportSetting.OverwriteUnmanagedCustomizations);

                OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                {
                    SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                    Message = string.Format("Updated Solution installation finished, status:{0}", result.ImportState)
                });

                //Extra to delete holding solution immediatelly
                if (!item.ImportSetting.DeleteOnly && !_configReader.DontUseHoldingSulutions)
                {
                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = "Holding Solution deletion started"
                    });

                    var result2 = item.SolutionImporter.DeleteHoldingSolution();

                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = result2
                    });
                }
                else
                {
                    OnRaiseImportUpdatEvent(new ImportUpdateEventArgs
                    {
                        SolutionDetails = item.SolutionImporter.GetSolutionDetails,
                        Message = "Holding Solution deletion not required"
                    });
                }
            }
        }

        #endregion Public Methods and Properties

        #region Internall class implementation

        private void ReadConfiguration()
        {
            foreach (var item in _configReader.SolutionImportSettings)
            {
                var pkgConfig = new SolutionPackage
                {
                    ImportSetting = item
                };

                var fileManager = new SolutionFileManager(Path.Combine(_configReader.SolutionsFolder, item.SolutionName), item.ForceUpgrade);

                pkgConfig.SolutionImporter = new SolutionImporter(fileManager, _importRepo, _configReader.UseNewApi);

                _packages.Add(pkgConfig);
            }
        }

        #endregion Internall class implementation
    }
}