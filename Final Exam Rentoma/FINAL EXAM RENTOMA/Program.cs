using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClinicAppointmentSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Clinic Appointment Management System";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("    CLINIC APPOINTMENT MANAGEMENT SYSTEM          ");
            Console.WriteLine("==================================================\n");
            Console.ResetColor();

            StorageInitializer.Initialize();
            MenuController menu = new MenuController();
            menu.Start();
        }
    }

    public class AppointmentRecord
    {
        public int RecordId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string Symptoms { get; set; } = string.Empty;
        public string AppointmentType { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Checksum { get; set; } = string.Empty;

        public AppointmentRecord()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            IsActive = true;
        }
    }

    class StorageInitializer
    {
        private const string DATA_DIR = "ClinicData";
        private const string RECORDS_FILE = "ClinicData/appointments.json";
        private const string AUDIT_FILE = "ClinicData/audit.log";
        private const string BACKUP_DIR = "ClinicData/backups";

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(DATA_DIR))
                {
                    Directory.CreateDirectory(DATA_DIR);
                    AuditLogger.Log("System", "Storage directory created", "INFO");
                }

                if (!Directory.Exists(BACKUP_DIR))
                {
                    Directory.CreateDirectory(BACKUP_DIR);
                    AuditLogger.Log("System", "Backup directory created", "INFO");
                }

                if (!File.Exists(RECORDS_FILE))
                {
                    File.WriteAllText(RECORDS_FILE, "[]");
                    AuditLogger.Log("System", "Records file created", "INFO");
                }

                if (!File.Exists(AUDIT_FILE))
                {
                    File.Create(AUDIT_FILE).Close();
                }

                AuditLogger.Log("System", "Storage initialization completed", "INFO");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing storage: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }

    class AuditLogger
    {
        private const string AUDIT_FILE = "ClinicData/audit.log";

        public static void Log(string user, string action, string status = "SUCCESS")
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {user,-10} | {action,-50} | {status}";
                File.AppendAllText(AUDIT_FILE, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audit log error: {ex.Message}");
            }
        }

        public static void ViewAuditTrail()
        {
            try
            {
                if (File.Exists(AUDIT_FILE))
                {
                    Console.Clear();
                    Console.WriteLine("=== AUDIT TRAIL LOG ===\n");
                    string[] logs = File.ReadAllLines(AUDIT_FILE);

                    if (logs.Length == 0)
                    {
                        Console.WriteLine("No audit logs found.");
                        return;
                    }

                    int startIndex = Math.Max(0, logs.Length - 20);
                    for (int i = startIndex; i < logs.Length; i++)
                    {
                        Console.WriteLine(logs[i]);
                    }

                    Console.WriteLine($"\nTotal entries: {logs.Length}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading audit log: {ex.Message}");
            }
        }
    }

    class Validator
    {
        public static (bool isValid, string errorMessage) ValidateAppointment(AppointmentRecord appointment)
        {
            if (string.IsNullOrWhiteSpace(appointment.PatientName))
                return (false, "Patient name is required");

            if (appointment.PatientName.Length < 2 || appointment.PatientName.Length > 50)
                return (false, "Patient name must be between 2 and 50 characters");

            if (string.IsNullOrWhiteSpace(appointment.DoctorName))
                return (false, "Doctor name is required");

            if (appointment.DoctorName.Length < 2 || appointment.DoctorName.Length > 40)
                return (false, "Doctor name must be between 2 and 40 characters");

            if (appointment.AppointmentDate < DateTime.Now.Date)
                return (false, "Appointment date cannot be in the past");

            if (appointment.AppointmentDate > DateTime.Now.AddMonths(3))
                return (false, "Appointment date cannot be more than 3 months in the future");

            if (string.IsNullOrWhiteSpace(appointment.Symptoms))
                return (false, "Symptoms description is required");

            if (appointment.Symptoms.Length < 5)
                return (false, "Symptoms description must be at least 5 characters");

            string[] validTypes = { "Checkup", "Emergency", "Follow-up", "Vaccination" };
            if (!validTypes.Contains(appointment.AppointmentType))
                return (false, "Invalid appointment type. Valid types: Checkup, Emergency, Follow-up, Vaccination");

            if (!string.IsNullOrWhiteSpace(appointment.ContactNumber))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(appointment.ContactNumber, @"^[0-9+\s\-\(\)]{8,15}$"))
                    return (false, "Invalid contact number format");
            }

            return (true, string.Empty);
        }

        public static bool ValidateId(string input, out int id)
        {
            return int.TryParse(input, out id) && id > 0;
        }
    }

    class ChecksumGenerator
    {
        public static string GenerateChecksum(AppointmentRecord record)
        {
            string data = $"{record.RecordId}|{record.PatientName}|{record.DoctorName}|{record.AppointmentDate}|{record.Symptoms}|{record.AppointmentType}|{record.ContactNumber}|{record.IsActive}";

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes).Substring(0, 16);
            }
        }

        public static bool VerifyChecksum(AppointmentRecord record)
        {
            string expectedChecksum = GenerateChecksum(record);
            return record.Checksum == expectedChecksum;
        }
    }

    class AppointmentRepository
    {
        private const string RECORDS_FILE = "ClinicData/appointments.json";
        private static int nextId = 1;
        private static List<AppointmentRecord> records = new List<AppointmentRecord>();

        static AppointmentRepository()
        {
            LoadFromFile();
        }

        private static void LoadFromFile()
        {
            try
            {
                if (File.Exists(RECORDS_FILE))
                {
                    string json = File.ReadAllText(RECORDS_FILE);
                    var loadedRecords = JsonSerializer.Deserialize<List<AppointmentRecord>>(json);
                    records = loadedRecords ?? new List<AppointmentRecord>();

                    if (records.Any())
                        nextId = records.Max(r => r.RecordId) + 1;
                    else
                        nextId = 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading records: {ex.Message}");
                records = new List<AppointmentRecord>();
            }
        }

        private static void SaveToFile()
        {
            try
            {
                if (File.Exists(RECORDS_FILE))
                {
                    string backupFile = $"ClinicData/backups/backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    File.Copy(RECORDS_FILE, backupFile, true);

                    var backupFiles = Directory.GetFiles("ClinicData/backups", "backup_*.json")
                        .OrderByDescending(f => f)
                        .Skip(10);
                    foreach (var file in backupFiles)
                    {
                        File.Delete(file);
                    }
                }

                string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(RECORDS_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving records: {ex.Message}");
                throw;
            }
        }

        public static AppointmentRecord Add(AppointmentRecord record)
        {
            record.RecordId = nextId++;
            record.CreatedAt = DateTime.Now;
            record.UpdatedAt = DateTime.Now;
            record.Checksum = ChecksumGenerator.GenerateChecksum(record);

            records.Add(record);
            SaveToFile();
            AuditLogger.Log("User", $"Added appointment: ID={record.RecordId}, Patient={record.PatientName}");
            return record;
        }

        public static List<AppointmentRecord> GetAll()
        {
            var activeRecords = records.Where(r => r.IsActive).ToList();
            AuditLogger.Log("User", $"Viewed all appointments ({activeRecords.Count} records)", "READ");
            return activeRecords;
        }

        public static AppointmentRecord? GetById(int id)
        {
            var foundRecord = records.FirstOrDefault(r => r.RecordId == id && r.IsActive);
            if (foundRecord != null)
                AuditLogger.Log("User", $"Retrieved appointment ID={id}", "READ");
            return foundRecord;
        }

        public static bool Update(int id, Action<AppointmentRecord> updateAction)
        {
            var foundRecord = records.FirstOrDefault(r => r.RecordId == id && r.IsActive);
            if (foundRecord == null) return false;

            updateAction(foundRecord);
            foundRecord.UpdatedAt = DateTime.Now;
            foundRecord.Checksum = ChecksumGenerator.GenerateChecksum(foundRecord);

            SaveToFile();
            AuditLogger.Log("User", $"Updated appointment ID={id}");
            return true;
        }

        public static bool SoftDelete(int id)
        {
            var foundRecord = records.FirstOrDefault(r => r.RecordId == id && r.IsActive);
            if (foundRecord == null) return false;

            foundRecord.IsActive = false;
            foundRecord.UpdatedAt = DateTime.Now;
            foundRecord.Checksum = ChecksumGenerator.GenerateChecksum(foundRecord);

            SaveToFile();
            AuditLogger.Log("User", $"Soft deleted appointment ID={id}");
            return true;
        }

        public static bool HardDelete(int id)
        {
            var foundRecord = records.FirstOrDefault(r => r.RecordId == id);
            if (foundRecord == null) return false;

            records.Remove(foundRecord);
            SaveToFile();
            AuditLogger.Log("User", $"Hard deleted appointment ID={id}", "WARNING");
            return true;
        }

        public static List<AppointmentRecord> Search(Func<AppointmentRecord, bool> predicate)
        {
            var results = records.Where(r => r.IsActive && predicate(r)).ToList();
            AuditLogger.Log("User", $"Search performed, found {results.Count} results", "READ");
            return results;
        }

        public static Dictionary<string, int> GetStatistics()
        {
            var activeRecords = records.Where(r => r.IsActive);
            var stats = new Dictionary<string, int>
            {
                ["Total"] = activeRecords.Count(),
                ["Checkup"] = activeRecords.Count(r => r.AppointmentType == "Checkup"),
                ["Emergency"] = activeRecords.Count(r => r.AppointmentType == "Emergency"),
                ["Follow-up"] = activeRecords.Count(r => r.AppointmentType == "Follow-up"),
                ["Vaccination"] = activeRecords.Count(r => r.AppointmentType == "Vaccination"),
                ["Today"] = activeRecords.Count(r => r.AppointmentDate.Date == DateTime.Now.Date),
                ["ThisWeek"] = activeRecords.Count(r => r.AppointmentDate.Date >= DateTime.Now.Date && r.AppointmentDate.Date <= DateTime.Now.AddDays(7).Date)
            };
            return stats;
        }
    }

    class ReportGenerator
    {
        public static void GenerateMonthlyReport(int year, int month)
        {
            try
            {
                var appointments = AppointmentRepository.GetAll();
                var monthlyAppointments = appointments.Where(a => a.AppointmentDate.Year == year && a.AppointmentDate.Month == month).ToList();

                string reportPath = $"ClinicData/report_{year}_{month:00}.txt";
                Directory.CreateDirectory("ClinicData");

                using (StreamWriter writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("==========================================================");
                    writer.WriteLine("         CLINIC APPOINTMENT MONTHLY REPORT                ");
                    writer.WriteLine("==========================================================");
                    writer.WriteLine();
                    writer.WriteLine($"Report Period: {year}-{month:00}");
                    writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine(new string('=', 70));
                    writer.WriteLine();
                    writer.WriteLine($"TOTAL APPOINTMENTS: {monthlyAppointments.Count}");
                    writer.WriteLine();

                    writer.WriteLine("APPOINTMENTS BY TYPE:");
                    writer.WriteLine(new string('-', 40));
                    var typeGroups = monthlyAppointments.GroupBy(a => a.AppointmentType);
                    foreach (var group in typeGroups)
                    {
                        double percentage = monthlyAppointments.Count > 0 ? (double)group.Count() / monthlyAppointments.Count * 100 : 0;
                        writer.WriteLine($"  - {group.Key,-12}: {group.Count(),3} ({percentage,5:F1}%)");
                    }
                    writer.WriteLine();

                    writer.WriteLine("APPOINTMENTS BY DOCTOR:");
                    writer.WriteLine(new string('-', 40));
                    var doctorGroups = monthlyAppointments.GroupBy(a => a.DoctorName);
                    foreach (var group in doctorGroups)
                    {
                        writer.WriteLine($"  - Dr. {group.Key,-15}: {group.Count(),3} appointments");
                    }
                    writer.WriteLine();

                    writer.WriteLine("DAILY BREAKDOWN:");
                    writer.WriteLine(new string('-', 40));
                    var dailyGroups = monthlyAppointments.GroupBy(a => a.AppointmentDate.Date);
                    foreach (var group in dailyGroups.OrderBy(g => g.Key))
                    {
                        writer.WriteLine($"  - {group.Key:yyyy-MM-dd} ({group.Key.DayOfWeek}): {group.Count()} appointments");
                    }
                    writer.WriteLine();

                    writer.WriteLine("DETAILED APPOINTMENT LIST:");
                    writer.WriteLine(new string('-', 90));
                    writer.WriteLine($"{"ID",-5} {"Patient",-22} {"Doctor",-15} {"Date",-12} {"Type",-12} {"Contact",-15}");
                    writer.WriteLine(new string('-', 90));

                    foreach (var app in monthlyAppointments.OrderBy(a => a.AppointmentDate))
                    {
                        writer.WriteLine($"{app.RecordId,-5} {app.PatientName,-22} {app.DoctorName,-15} {app.AppointmentDate:yyyy-MM-dd,-12} {app.AppointmentType,-12} {(app.ContactNumber ?? "N/A"),-15}");
                    }

                    writer.WriteLine();
                    writer.WriteLine(new string('=', 70));
                    writer.WriteLine("End of Report");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nMonthly report generated successfully!");
                Console.WriteLine($"File: {reportPath}");
                Console.ResetColor();
                AuditLogger.Log("System", $"Generated monthly report for {year}-{month:00}");

                Console.Write("\nDo you want to view the report? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    Console.Clear();
                    if (File.Exists(reportPath))
                        Console.WriteLine(File.ReadAllText(reportPath));
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError generating report: {ex.Message}");
                Console.ResetColor();
                AuditLogger.Log("System", $"Report generation failed: {ex.Message}", "ERROR");
            }
        }

        public static void GenerateStatisticsReport()
        {
            try
            {
                var stats = AppointmentRepository.GetStatistics();
                string reportPath = $"ClinicData/statistics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                using (StreamWriter writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("==========================================================");
                    writer.WriteLine("            CLINIC STATISTICS REPORT                     ");
                    writer.WriteLine("==========================================================");
                    writer.WriteLine();
                    writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine(new string('=', 60));
                    writer.WriteLine();
                    writer.WriteLine($"Total Active Records: {stats["Total"]}");
                    writer.WriteLine();
                    writer.WriteLine("Appointments by Type:");
                    writer.WriteLine($"   - Checkup:      {stats["Checkup"]}");
                    writer.WriteLine($"   - Emergency:    {stats["Emergency"]}");
                    writer.WriteLine($"   - Follow-up:    {stats["Follow-up"]}");
                    writer.WriteLine($"   - Vaccination:  {stats["Vaccination"]}");
                    writer.WriteLine();
                    writer.WriteLine("Upcoming Appointments:");
                    writer.WriteLine($"   - Today:        {stats["Today"]}");
                    writer.WriteLine($"   - This Week:    {stats["ThisWeek"]}");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nStatistics report generated: {reportPath}");
                Console.ResetColor();
                AuditLogger.Log("System", "Generated statistics report");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError generating statistics report: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    class MenuController
    {
        public void Start()
        {
            while (true)
            {
                DisplayMainMenu();
                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        AddRecord();
                        break;
                    case "2":
                        ViewRecords();
                        break;
                    case "3":
                        UpdateRecord();
                        break;
                    case "4":
                        DeleteRecordMenu();
                        break;
                    case "5":
                        ReportMenu();
                        break;
                    case "6":
                        AuditLogger.ViewAuditTrail();
                        break;
                    case "7":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\nExiting application... Goodbye!");
                        Console.ResetColor();
                        AuditLogger.Log("User", "Application exited");
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nInvalid option. Please try again.");
                        Console.ResetColor();
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }

        private void DisplayMainMenu()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================================");
            Console.WriteLine("                    MAIN MENU                              ");
            Console.WriteLine("==========================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  1. Add New Appointment");
            Console.WriteLine("  2. View Appointments");
            Console.WriteLine("  3. Update Appointment");
            Console.WriteLine("  4. Delete Appointment");
            Console.WriteLine("  5. Generate Reports");
            Console.WriteLine("  6. View Audit Trail");
            Console.WriteLine("  7. Exit");
            Console.WriteLine();
            Console.Write("Select option (1-7): ");
        }

        private void AddRecord()
        {
            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                  ADD NEW APPOINTMENT                     ");
            Console.WriteLine("==========================================================\n");

            try
            {
                AppointmentRecord newAppointment = new AppointmentRecord();

                Console.Write("Patient Name: ");
                newAppointment.PatientName = Console.ReadLine() ?? string.Empty;

                Console.Write("Doctor Name: ");
                newAppointment.DoctorName = Console.ReadLine() ?? string.Empty;

                Console.Write("Appointment Date (yyyy-mm-dd): ");
                if (DateTime.TryParse(Console.ReadLine(), out DateTime date))
                    newAppointment.AppointmentDate = date;
                else
                    throw new Exception("Invalid date format. Please use yyyy-mm-dd");

                Console.Write("Symptoms: ");
                newAppointment.Symptoms = Console.ReadLine() ?? string.Empty;

                Console.Write("Appointment Type (Checkup/Emergency/Follow-up/Vaccination): ");
                newAppointment.AppointmentType = Console.ReadLine() ?? string.Empty;

                Console.Write("Contact Number (optional): ");
                newAppointment.ContactNumber = Console.ReadLine();

                var validation = Validator.ValidateAppointment(newAppointment);
                if (!validation.isValid)
                {
                    throw new Exception(validation.errorMessage);
                }

                var saved = AppointmentRepository.Add(newAppointment);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n========== APPOINTMENT ADDED SUCCESSFULLY! ==========");
                Console.WriteLine($"  RECORD ID: {saved.RecordId}  <--- SAVE THIS ID");
                Console.WriteLine($"  Patient:   {saved.PatientName}");
                Console.WriteLine($"  Doctor:    {saved.DoctorName}");
                Console.WriteLine($"  Date:      {saved.AppointmentDate:yyyy-MM-dd}");
                Console.WriteLine($"  Type:      {saved.AppointmentType}");
                Console.WriteLine("======================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {ex.Message}");
                Console.ResetColor();
                AuditLogger.Log("System", $"Add record failed: {ex.Message}", "ERROR");
            }
        }

        private void ViewRecords()
        {
            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                   VIEW APPOINTMENTS                       ");
            Console.WriteLine("==========================================================\n");
            Console.WriteLine("Search Options:");
            Console.WriteLine("  1. Show All Appointments");
            Console.WriteLine("  2. Search by Patient Name");
            Console.WriteLine("  3. Search by Doctor");
            Console.WriteLine("  4. Search by Date Range");
            Console.WriteLine("  5. Search by Appointment Type");
            Console.Write("\nSelect option: ");

            string? option = Console.ReadLine();
            List<AppointmentRecord> results = new List<AppointmentRecord>();

            switch (option)
            {
                case "1":
                    results = AppointmentRepository.GetAll();
                    break;
                case "2":
                    Console.Write("\nEnter patient name: ");
                    string? patientName = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(patientName))
                        results = AppointmentRepository.Search(a => a.PatientName.Contains(patientName, StringComparison.OrdinalIgnoreCase));
                    break;
                case "3":
                    Console.Write("\nEnter doctor name: ");
                    string? doctorName = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(doctorName))
                        results = AppointmentRepository.Search(a => a.DoctorName.Contains(doctorName, StringComparison.OrdinalIgnoreCase));
                    break;
                case "4":
                    Console.Write("\nStart date (yyyy-mm-dd): ");
                    if (!DateTime.TryParse(Console.ReadLine(), out DateTime start))
                    {
                        Console.WriteLine("Invalid date format.");
                        return;
                    }
                    Console.Write("End date (yyyy-mm-dd): ");
                    if (!DateTime.TryParse(Console.ReadLine(), out DateTime end))
                    {
                        Console.WriteLine("Invalid date format.");
                        return;
                    }
                    results = AppointmentRepository.Search(a => a.AppointmentDate.Date >= start.Date && a.AppointmentDate.Date <= end.Date);
                    break;
                case "5":
                    Console.Write("\nAppointment Type (Checkup/Emergency/Follow-up/Vaccination): ");
                    string? type = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(type))
                        results = AppointmentRepository.Search(a => a.AppointmentType.Equals(type, StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    Console.WriteLine("Invalid option");
                    return;
            }

            DisplayRecords(results);
        }

        private void DisplayRecords(List<AppointmentRecord> records)
        {
            if (!records.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nNo records found.");
                Console.ResetColor();
                return;
            }

            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                  APPOINTMENT RECORDS                     ");
            Console.WriteLine("==========================================================\n");

            Console.WriteLine($"{"ID",-5} {"Patient",-25} {"Doctor",-15} {"Date",-12} {"Type",-12} {"Status",-10}");
            Console.WriteLine(new string('-', 90));

            foreach (var r in records)
            {
                bool checksumValid = ChecksumGenerator.VerifyChecksum(r);
                string status = r.IsActive ? (checksumValid ? "Active" : "Corrupted") : "Deleted";
                ConsoleColor statusColor = checksumValid ? ConsoleColor.Green : ConsoleColor.Red;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{r.RecordId,-5}");
                Console.ResetColor();

                Console.ForegroundColor = statusColor;
                Console.WriteLine($" {r.PatientName,-25} {r.DoctorName,-15} " +
                    $"{r.AppointmentDate:yyyy-MM-dd,-12} {r.AppointmentType,-12} {status,-10}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nTotal Records: {records.Count}");
            Console.WriteLine("Tip: Use the ID numbers above (first column) for updates and deletions");
            Console.ResetColor();
        }

        private void UpdateRecord()
        {
            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                   UPDATE APPOINTMENT                      ");
            Console.WriteLine("==========================================================\n");

            var allRecords = AppointmentRepository.GetAll();
            if (allRecords.Any())
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Recent Records (showing last 10):");
                Console.WriteLine("==========================================================");
                Console.ResetColor();
                Console.WriteLine($"{"ID",-5} {"Patient",-25} {"Doctor",-15} {"Date",-12}");
                Console.WriteLine(new string('-', 60));

                foreach (var r in allRecords.Take(10))
                {
                    Console.WriteLine($"{r.RecordId,-5} {r.PatientName,-25} {r.DoctorName,-15} {r.AppointmentDate:yyyy-MM-dd,-12}");
                }
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No records found. Please add a record first.");
                Console.ResetColor();
                return;
            }

            Console.Write("Enter Record ID to update (from the list above): ");
            if (!Validator.ValidateId(Console.ReadLine() ?? string.Empty, out int id))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid ID format.");
                Console.ResetColor();
                return;
            }

            var existingRecord = AppointmentRepository.GetById(id);
            if (existingRecord == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Record with ID {id} not found or is inactive.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n==========================================================");
            Console.WriteLine("              CURRENT RECORD INFORMATION                   ");
            Console.WriteLine("==========================================================");
            Console.ResetColor();
            Console.WriteLine($"ID:           {existingRecord.RecordId}");
            Console.WriteLine($"Patient:      {existingRecord.PatientName}");
            Console.WriteLine($"Doctor:       {existingRecord.DoctorName}");
            Console.WriteLine($"Date:         {existingRecord.AppointmentDate:yyyy-MM-dd}");
            Console.WriteLine($"Symptoms:     {existingRecord.Symptoms}");
            Console.WriteLine($"Type:         {existingRecord.AppointmentType}");
            Console.WriteLine($"Contact:      {existingRecord.ContactNumber ?? "N/A"}");
            Console.WriteLine($"Created:      {existingRecord.CreatedAt:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Last Updated: {existingRecord.UpdatedAt:yyyy-MM-dd HH:mm}");

            string newPatientName = existingRecord.PatientName;
            string newDoctorName = existingRecord.DoctorName;
            DateTime newDate = existingRecord.AppointmentDate;
            string newSymptoms = existingRecord.Symptoms;
            string newType = existingRecord.AppointmentType;
            string? newContact = existingRecord.ContactNumber;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n==========================================================");
            Console.WriteLine("        ENTER NEW VALUES (press Enter to keep)             ");
            Console.WriteLine("==========================================================");
            Console.ResetColor();

            Console.Write($"\nPatient Name [{existingRecord.PatientName}]: ");
            string? input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                newPatientName = input;

            Console.Write($"Doctor Name [{existingRecord.DoctorName}]: ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                newDoctorName = input;

            Console.Write($"Appointment Date [{existingRecord.AppointmentDate:yyyy-MM-dd}]: ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                if (DateTime.TryParse(input, out DateTime parsedDate))
                    newDate = parsedDate;
                else
                    Console.WriteLine("Invalid date format. Keeping current date.");
            }

            Console.Write($"Symptoms [{existingRecord.Symptoms}]: ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                newSymptoms = input;

            Console.Write($"Appointment Type [{existingRecord.AppointmentType}] (Checkup/Emergency/Follow-up/Vaccination): ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                newType = input;

            Console.Write($"Contact Number [{existingRecord.ContactNumber ?? "N/A"}]: ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
                newContact = input;

            var tempRecord = new AppointmentRecord
            {
                PatientName = newPatientName,
                DoctorName = newDoctorName,
                AppointmentDate = newDate,
                Symptoms = newSymptoms,
                AppointmentType = newType,
                ContactNumber = newContact
            };

            var validation = Validator.ValidateAppointment(tempRecord);
            if (!validation.isValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nValidation Error: {validation.errorMessage}");
                Console.ResetColor();
                return;
            }

            Console.Write("\nConfirm update? (y/n): ");
            if (Console.ReadLine()?.ToLower() != "y")
            {
                Console.WriteLine("Update cancelled.");
                return;
            }

            bool updated = AppointmentRepository.Update(id, r =>
            {
                r.PatientName = newPatientName;
                r.DoctorName = newDoctorName;
                r.AppointmentDate = newDate;
                r.Symptoms = newSymptoms;
                r.AppointmentType = newType;
                r.ContactNumber = newContact;
            });

            if (updated)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nRecord updated successfully!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nUpdate failed.");
                Console.ResetColor();
            }
        }

        private void DeleteRecordMenu()
        {
            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                   DELETE APPOINTMENT                      ");
            Console.WriteLine("==========================================================\n");

            var allRecords = AppointmentRepository.GetAll();
            if (allRecords.Any())
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Active Records:");
                Console.WriteLine("==========================================================");
                Console.ResetColor();
                Console.WriteLine($"{"ID",-5} {"Patient",-25} {"Doctor",-15} {"Date",-12} {"Type",-12}");
                Console.WriteLine(new string('-', 75));

                foreach (var r in allRecords)
                {
                    Console.WriteLine($"{r.RecordId,-5} {r.PatientName,-25} {r.DoctorName,-15} {r.AppointmentDate:yyyy-MM-dd,-12} {r.AppointmentType,-12}");
                }
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No active records found.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("  1. Soft Delete (Mark as inactive)");
            Console.WriteLine("  2. Hard Delete (Permanently remove)");
            Console.Write("\nSelect option: ");

            string? option = Console.ReadLine();
            Console.Write("Enter Record ID to delete (from list above): ");

            if (!Validator.ValidateId(Console.ReadLine() ?? string.Empty, out int id))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid ID format.");
                Console.ResetColor();
                return;
            }

            var foundRecord = AppointmentRepository.GetById(id);
            if (foundRecord == null && option != "2")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Record with ID {id} not found.");
                Console.ResetColor();
                return;
            }

            bool success = false;
            if (option == "1")
            {
                if (foundRecord != null)
                {
                    Console.Write($"\nDelete '{foundRecord.PatientName}' (ID: {id})? (y/n): ");
                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        success = AppointmentRepository.SoftDelete(id);
                        if (success)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\nRecord ID {id} soft deleted.");
                            Console.ResetColor();
                        }
                    }
                }
            }
            else if (option == "2")
            {
                Console.Write($"\nType 'DELETE' to permanently remove record {id}: ");
                if (Console.ReadLine() == "DELETE")
                {
                    success = AppointmentRepository.HardDelete(id);
                    if (success)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\nRecord ID {id} permanently deleted.");
                        Console.ResetColor();
                    }
                }
            }

            if (!success && option != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nDelete failed.");
                Console.ResetColor();
            }
        }

        private void ReportMenu()
        {
            Console.Clear();
            Console.WriteLine("==========================================================");
            Console.WriteLine("                   GENERATE REPORTS                        ");
            Console.WriteLine("==========================================================\n");
            Console.WriteLine("  1. Monthly Report");
            Console.WriteLine("  2. Statistics Report");
            Console.Write("\nSelect option: ");

            string? option = Console.ReadLine();

            if (option == "1")
            {
                Console.Write("\nEnter year (yyyy): ");
                if (!int.TryParse(Console.ReadLine(), out int year) || year < 2020 || year > DateTime.Now.Year + 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid year.");
                    Console.ResetColor();
                    return;
                }

                Console.Write("Enter month (1-12): ");
                if (!int.TryParse(Console.ReadLine(), out int month) || month < 1 || month > 12)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid month.");
                    Console.ResetColor();
                    return;
                }

                ReportGenerator.GenerateMonthlyReport(year, month);
            }
            else if (option == "2")
            {
                ReportGenerator.GenerateStatisticsReport();
            }
            else
            {
                Console.WriteLine("Invalid option.");
            }
        }
    }
}