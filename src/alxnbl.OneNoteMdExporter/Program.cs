﻿using alxnbl.OneNoteMdExporter.Helpers;
using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using CommandLine;
using OneNote = Microsoft.Office.Interop.OneNote;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace alxnbl.OneNoteMdExporter
{
    public class Program
    {
        public class Options
        {
            [Option('n', "notebook", Required = false, HelpText = "The name of the notebook to export")]
            public string NotebookName { get; set; }

            [Option('f', "format", Required = false, HelpText = "The format of export : 1 for Markdown folder ; 2 for Joplin folder")]
            public string ExportFormat { get; set; }

            [Option('s', "section", Required = false, HelpText = "The name of the section to export. Apply only if notebook parameter used.")]
            public string SectionName { get; set; }

            [Option('p', "page", Required = false, HelpText = "The name of the section page to export. Apply only if section parameter used.")]
            public string PageName { get; set; }
        }


        public static void Main(params string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(options => {
                       if (string.IsNullOrEmpty(options.NotebookName))
                           options.SectionName = string.Empty;
                       if (string.IsNullOrEmpty(options.SectionName))
                           options.PageName = string.Empty;

                       RunOptions(options);
                    });
        }

        private static OneNote.Application OneNoteApp;

        private static void RunOptions(Options opts)
        {
            var appSettings = AppSettings.LoadAppSettings();
            InitLogger();

            OneNoteApp = new OneNote.Application();


            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

            Log.Debug($"CurrentCulture : {CultureInfo.CurrentCulture}");
            Log.Debug($"OneNoteMdExporter version {assemblyVersion}");

            Log.Information("-----------------------");
            Log.Information("- OneNote Md Exporter -");
            Log.Information($"       v{assemblyVersion}");
            Log.Information("-----------------------\n");


            IList<Notebook> notebookToProcess;

            if (string.IsNullOrEmpty(opts.NotebookName))
            {
                // Request user to select notebooks to export
                notebookToProcess = NotebookSelectionForm();
            }
            else
            {
                // Notebook name provided in args
                notebookToProcess = GetNotebookFromName(opts.NotebookName);
            }

            if (notebookToProcess.Count == 0)
                return;

            ExportFormat exportFormat = ExportFormatSelectionForm(opts.ExportFormat);

            if (exportFormat == ExportFormat.Undefined)
                return;

            var exportService = ExportServiceFactory.GetExportService(exportFormat, appSettings, OneNoteApp);

            foreach (Notebook notebook in notebookToProcess)
            {
                Log.Information("\n***************************************");
                Log.Information(Localizer.GetString("StartExportingNotebook"), notebook.Title);
                Log.Information("***************************************");

                exportService.ExportNotebook(notebook, opts.SectionName, opts.PageName);
                var exportPath = Path.GetFullPath(notebook.Title);

                Log.Information("");
                Log.Information(Localizer.GetString("ExportSuccessful"), exportPath);
                Log.Information("");
            }


            Log.Information(Localizer.GetString("EndOfExport"));

            Console.ReadLine();
        }

        private static ExportFormat ExportFormatSelectionForm(string optsExportFormat = "")
        {
            if (string.IsNullOrEmpty(optsExportFormat))
            {
                Log.Information(Localizer.GetString("ChooseExportFormat"));
                Log.Information(Localizer.GetString("ChooseExportFormat1"));
                Log.Information(Localizer.GetString("ChooseExportFormat2"));

                optsExportFormat = Console.ReadLine();

                Log.Information("");
            }


            if (!Enum.TryParse<ExportFormat>(optsExportFormat, true, out var exportFormat))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return ExportFormat.Undefined;
            }

            return exportFormat;
        }

        private static IList<Notebook> GetNotebookFromName(string notebookName)
        {
            var notebook = OneNoteApp.GetNotebooks().Where(n => n.Title == notebookName).ToList(); // can be optimized

            if(notebook.Count == 0)
                Log.Information(Localizer.GetString("NotebookNameNotFound"), notebookName);

            return notebook;
        }

        private static IList<Notebook> NotebookSelectionForm()
        {
            var notebooks = OneNoteApp.GetNotebooks();

            Log.Information(Localizer.GetString("NotebookFounds"), notebooks.Count);
            Log.Information(Localizer.GetString("ExportAllNotebooks"));

            for (int i = 1; i <= notebooks.Count; i++)
            {
                Log.Information(Localizer.GetString("ExportNotebookPositionX"), i, notebooks.ElementAt(i - 1).Title);
            }

            var input = Console.ReadLine();

            if (!Int32.TryParse(input, out var inputInt))
            {
                Log.Information(Localizer.GetString("BadInput"));
                return new List<Notebook>();
            }

            if (inputInt == 0)
            {
                return notebooks;
            }
            else
            {
                try
                {
                    return new List<Notebook> { notebooks.ElementAt(inputInt - 1) };
                }
                catch (ArgumentOutOfRangeException)
                {
                    Log.Information(Localizer.GetString("NotebookNotFound"));
                    return new List<Notebook>(); ;
                }
            }
        }

        private static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .WriteTo.File("OneNoteMdExporter.log")
               .WriteTo.Console(Serilog.Events.LogEventLevel.Information, "{Message:lj}{NewLine}")
               .CreateLogger();
        }
    }
}