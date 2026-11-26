using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using QuickPos.Components;

namespace QuickPos.Services;

/// <summary>
/// Local SQLite database service storing Products, Bills, and BillItems in %LOCALAPPDATA%\QuickPOS\quickpos.db.
/// All tables start completely empty.
/// </summary>
public static class DatabaseService
{
    private static readonly string DbDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickPOS");

    private static readonly string DbPath = Path.Combine(DbDirectory, "quickpos.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        Directory.CreateDirectory(DbDirectory);

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                Alias TEXT NOT NULL DEFAULT '',
                Stock REAL NOT NULL DEFAULT 0,
                Rate REAL NOT NULL DEFAULT 0,
                Gst TEXT NOT NULL DEFAULT '8%'
            );

            CREATE TABLE IF NOT EXISTS Bills (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BillNo TEXT NOT NULL,
                Prefix TEXT NOT NULL,
                BillDate TEXT NOT NULL,
                Amount REAL NOT NULL,
                CustCode TEXT NOT NULL DEFAULT '',
                Customer TEXT NOT NULL DEFAULT '',
                User TEXT NOT NULL DEFAULT 'SALES',
                PrintStatus TEXT NOT NULL DEFAULT 'No',
                TenderType TEXT NOT NULL DEFAULT 'Cash',
                Rrn TEXT NOT NULL DEFAULT '',
                IsReturn INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS BillItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BillId INTEGER NOT NULL,
                Code TEXT NOT NULL,
                Item TEXT NOT NULL,
                Rate REAL NOT NULL,
                Qty INTEGER NOT NULL,
                DiscPct REAL NOT NULL DEFAULT 0,
                ReturnedQty INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(BillId) REFERENCES Bills(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Phone TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                CustCode TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS PurchaseInvoices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrnNo TEXT NOT NULL,
                GrnDate TEXT NOT NULL,
                InvoiceNo TEXT NOT NULL DEFAULT '',
                Distributor TEXT NOT NULL DEFAULT '',
                Amount REAL NOT NULL DEFAULT 0,
                Narration TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS PurchaseItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PurchaseId INTEGER NOT NULL,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                Qty INTEGER NOT NULL DEFAULT 0,
                Free INTEGER NOT NULL DEFAULT 0,
                PurRate REAL NOT NULL DEFAULT 0,
                NetAmt REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (PurchaseId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Distributors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Address TEXT NOT NULL DEFAULT '',
                Phone TEXT NOT NULL DEFAULT '',
                GstNo TEXT NOT NULL DEFAULT '',
                DistType TEXT NOT NULL DEFAULT 'Local',
                CreditDays INTEGER NOT NULL DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();

        // Safe migration for existing DBs created before ReturnedQty was added
        try
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE BillItems ADD COLUMN ReturnedQty INTEGER NOT NULL DEFAULT 0;";
            alterCmd.ExecuteNonQuery();
        }
        catch { }

        // Safe migrations for extended Distributor Master fields
        string[] distAlterCols =
        [
            "ALTER TABLE Distributors ADD COLUMN ShortName TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Location TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Landmark TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN City TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN State TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Country TEXT NOT NULL DEFAULT 'India';",
            "ALTER TABLE Distributors ADD COLUMN PinCode TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN TelOffice TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Email TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Fax TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN CurrencyName TEXT NOT NULL DEFAULT 'INR';",
            "ALTER TABLE Distributors ADD COLUMN TelRes TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN DistributorId TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Area TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN BarcodeAlias TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN ContactPerson TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN PanNo TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN GstRegType TEXT NOT NULL DEFAULT 'Regular';",
            "ALTER TABLE Distributors ADD COLUMN OrderDays TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Distributors ADD COLUMN Comments TEXT NOT NULL DEFAULT '';"
        ];
        foreach (string sql in distAlterCols)
        {
            try
            {
                using var alterDist = conn.CreateCommand();
                alterDist.CommandText = sql;
                alterDist.ExecuteNonQuery();
            }
            catch { }
        }

        string[] productAndPurchaseAlterCols =
        [
            "ALTER TABLE Products ADD COLUMN Barcode TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Products ADD COLUMN Size TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Products ADD COLUMN HsnCode TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Products ADD COLUMN Brand TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE Products ADD COLUMN Style TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE PurchaseItems ADD COLUMN Barcode TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE PurchaseItems ADD COLUMN Size TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE PurchaseItems ADD COLUMN HsnCode TEXT NOT NULL DEFAULT '';"
        ];
        foreach (string sql in productAndPurchaseAlterCols)
        {
            try
            {
                using var alterCmd2 = conn.CreateCommand();
                alterCmd2.CommandText = sql;
                alterCmd2.ExecuteNonQuery();
            }
            catch { }
        }
    }

    public static (string Phone, string Name, string CustCode)? GetCustomerByPhone(string phone)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Phone, Name, CustCode FROM Customers WHERE Phone = $phone LIMIT 1;";
        cmd.Parameters.AddWithValue("$phone", phone.Trim());

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    public static string UpsertCustomer(string phone, string name)
    {
        EnsureInitialized();
        phone = phone.Trim();
        name = name.Trim();

        var existing = GetCustomerByPhone(phone);
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        if (existing.HasValue)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE Customers SET Name = $name WHERE Phone = $phone;";
            updateCmd.Parameters.AddWithValue("$name", name);
            updateCmd.Parameters.AddWithValue("$phone", phone);
            updateCmd.ExecuteNonQuery();
            return existing.Value.CustCode;
        }

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Customers;";
        long count = (long)(countCmd.ExecuteScalar() ?? 0L);
        string custCode = (count + 1).ToString();

        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO Customers (Phone, Name, CustCode)
            VALUES ($phone, $name, $custCode);";
        insertCmd.Parameters.AddWithValue("$phone", phone);
        insertCmd.Parameters.AddWithValue("$name", name);
        insertCmd.Parameters.AddWithValue("$custCode", custCode);
        insertCmd.ExecuteNonQuery();

        return custCode;
    }

    public static List<CustomerRecord> GetCustomers()
    {
        EnsureInitialized();
        var list = new List<CustomerRecord>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CustCode, Name, Phone FROM Customers ORDER BY Name COLLATE NOCASE ASC;";
        using var reader = cmd.ExecuteReader();

        int sno = 1;
        while (reader.Read())
        {
            list.Add(new CustomerRecord
            {
                Sno = sno++,
                CustCode = reader.GetString(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2)
            });
        }
        return list;
    }

    public static List<ProductItem> GetProducts()
    {
        EnsureInitialized();
        var list = new List<ProductItem>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code, Name, Alias, Stock, Rate, Gst, Barcode, Size, HsnCode, Brand, Style FROM Products ORDER BY Id ASC;";
        using var reader = cmd.ExecuteReader();

        int sno = 1;
        while (reader.Read())
        {
            list.Add(new ProductItem
            {
                Sno = sno++,
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                Alias = reader.GetString(2),
                Stock = (decimal)reader.GetDouble(3),
                Rate = (decimal)reader.GetDouble(4),
                Gst = reader.GetString(5),
                Barcode = reader.GetString(6),
                Size = reader.GetString(7),
                HsnCode = reader.GetString(8),
                Brand = reader.GetString(9),
                Style = reader.GetString(10)
            });
        }
        return list;
    }

    public static ProductItem? GetProductByBarcodeOrCode(string codeOrBarcode)
    {
        EnsureInitialized();
        string q = codeOrBarcode.Trim();
        if (string.IsNullOrEmpty(q)) return null;

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Code, Name, Alias, Stock, Rate, Gst, Barcode, Size, HsnCode, Brand, Style
            FROM Products
            WHERE Barcode = $q COLLATE NOCASE
               OR Code = $q COLLATE NOCASE
               OR Alias = $q COLLATE NOCASE
            LIMIT 1;";
        cmd.Parameters.AddWithValue("$q", q);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ProductItem
            {
                Sno = 1,
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                Alias = reader.GetString(2),
                Stock = (decimal)reader.GetDouble(3),
                Rate = (decimal)reader.GetDouble(4),
                Gst = reader.GetString(5),
                Barcode = reader.GetString(6),
                Size = reader.GetString(7),
                HsnCode = reader.GetString(8),
                Brand = reader.GetString(9),
                Style = reader.GetString(10)
            };
        }
        return null;
    }

    public static string GetNextProductCode()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) + 101 FROM Products;";
        var next = Convert.ToInt32(cmd.ExecuteScalar() ?? 101);
        return next.ToString();
    }

    public static string GetNextInternalBarcode()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) + 1 FROM Products;";
        int next = Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
        string candidate = $"20{next:D4}";

        // Ensure uniqueness if custom barcodes were entered
        while (true)
        {
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT COUNT(*) FROM Products WHERE Barcode = $b;";
            chk.Parameters.AddWithValue("$b", candidate);
            long exists = (long)(chk.ExecuteScalar() ?? 0L);
            if (exists == 0) return candidate;
            next++;
            candidate = $"20{next:D4}";
        }
    }

    public static void AddProduct(
        string code,
        string name,
        string alias,
        decimal stock,
        decimal rate,
        string gst = "8%",
        string barcode = "",
        string size = "",
        string hsnCode = "",
        string brand = "",
        string style = "")
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Products (Code, Name, Alias, Stock, Rate, Gst, Barcode, Size, HsnCode, Brand, Style)
            VALUES ($code, $name, $alias, $stock, $rate, $gst, $barcode, $size, $hsnCode, $brand, $style);";
        cmd.Parameters.AddWithValue("$code", code);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$alias", alias);
        cmd.Parameters.AddWithValue("$stock", (double)stock);
        cmd.Parameters.AddWithValue("$rate", (double)rate);
        cmd.Parameters.AddWithValue("$gst", gst);
        cmd.Parameters.AddWithValue("$barcode", barcode.Trim());
        cmd.Parameters.AddWithValue("$size", size.Trim());
        cmd.Parameters.AddWithValue("$hsnCode", hsnCode.Trim());
        cmd.Parameters.AddWithValue("$brand", brand.Trim());
        cmd.Parameters.AddWithValue("$style", style.Trim());
        cmd.ExecuteNonQuery();
    }

    public static void DeductStock(string code, int qty)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Products
            SET Stock = CASE WHEN Stock - $qty < 0 THEN 0 ELSE Stock - $qty END
            WHERE Code = $code;";
        cmd.Parameters.AddWithValue("$qty", qty);
        cmd.Parameters.AddWithValue("$code", code);
        cmd.ExecuteNonQuery();
    }

    public static void RestoreStock(string code, int qty)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Products
            SET Stock = Stock + $qty
            WHERE Code = $code;";
        cmd.Parameters.AddWithValue("$qty", qty);
        cmd.Parameters.AddWithValue("$code", code);
        cmd.ExecuteNonQuery();
    }

    public static int GetNextBillNumberByPrefix(string prefix)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Bills WHERE Prefix = $prefix;";
        cmd.Parameters.AddWithValue("$prefix", prefix);
        long count = (long)(cmd.ExecuteScalar() ?? 0L);
        return (int)count + 1;
    }

    public static int GetNextSalesBillNumber()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Bills WHERE IsReturn = 0;";
        long count = (long)(cmd.ExecuteScalar() ?? 0L);
        return (int)count + 1;
    }

    public static int GetNextReturnBillNumber()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Bills WHERE IsReturn = 1;";
        long count = (long)(cmd.ExecuteScalar() ?? 0L);
        return (int)count + 1;
    }

    public static List<SavedBillRecord> GetBills(int limit = 50)
    {
        EnsureInitialized();
        var bills = new List<SavedBillRecord>();
        var byId = new Dictionary<long, SavedBillRecord>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Id, BillNo, Prefix, BillDate, Amount, CustCode, Customer, User, PrintStatus, IsReturn
                FROM Bills
                ORDER BY Id DESC
                LIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            int sno = 1;
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                var rec = new SavedBillRecord
                {
                    Sno = sno++,
                    BillNo = reader.GetString(1),
                    Prefix = reader.GetString(2),
                    Date = reader.GetString(3),
                    Amount = (decimal)reader.GetDouble(4),
                    CustCode = reader.GetString(5),
                    Customer = reader.GetString(6),
                    User = reader.GetString(7),
                    Print = reader.GetString(8),
                    IsReturn = reader.GetInt32(9) == 1
                };
                bills.Add(rec);
                byId[id] = rec;
            }
        }

        if (byId.Count > 0)
        {
            using var itemCmd = conn.CreateCommand();
            string ids = string.Join(",", byId.Keys);
            itemCmd.CommandText = $"SELECT BillId, Code, Item, Rate, Qty, DiscPct, ReturnedQty FROM BillItems WHERE BillId IN ({ids});";
            using var reader = itemCmd.ExecuteReader();
            while (reader.Read())
            {
                long billId = reader.GetInt64(0);
                if (byId.TryGetValue(billId, out var rec))
                {
                    rec.Items.Add(new SavedBillItem
                    {
                        Code = reader.GetString(1),
                        Item = reader.GetString(2),
                        Rate = (decimal)reader.GetDouble(3),
                        Qty = reader.GetInt32(4),
                        DiscPct = (decimal)reader.GetDouble(5),
                        ReturnedQty = reader.GetInt32(6)
                    });
                }
            }
        }

        return bills;
    }

    public static void MarkItemsReturned(string originalBillNo, IEnumerable<(string Code, int Qty)> returnedItems)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        long billId = 0;
        using (var findCmd = conn.CreateCommand())
        {
            findCmd.Transaction = tx;
            findCmd.CommandText = "SELECT Id FROM Bills WHERE BillNo = $billNo AND IsReturn = 0 ORDER BY Id DESC LIMIT 1;";
            findCmd.Parameters.AddWithValue("$billNo", originalBillNo);
            var scalar = findCmd.ExecuteScalar();
            if (scalar != null && scalar != DBNull.Value)
                billId = (long)scalar;
        }

        if (billId > 0)
        {
            foreach (var (code, qty) in returnedItems)
            {
                using var updCmd = conn.CreateCommand();
                updCmd.Transaction = tx;
                updCmd.CommandText = @"
                    UPDATE BillItems
                    SET ReturnedQty = MIN(Qty, ReturnedQty + $qty)
                    WHERE BillId = $billId AND Code = $code;";
                updCmd.Parameters.AddWithValue("$qty", qty);
                updCmd.Parameters.AddWithValue("$billId", billId);
                updCmd.Parameters.AddWithValue("$code", code);
                updCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public static void AddBill(
        string billNo,
        string prefix,
        string date,
        decimal amount,
        string custCode,
        string customer,
        bool printed,
        string tenderType,
        string rrn,
        bool isReturn,
        IEnumerable<SavedBillItem> items)
    {
        EnsureInitialized();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        long billId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Bills (BillNo, Prefix, BillDate, Amount, CustCode, Customer, User, PrintStatus, TenderType, Rrn, IsReturn)
                VALUES ($billNo, $prefix, $date, $amount, $custCode, $customer, 'SALES', $printStatus, $tenderType, $rrn, $isReturn);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$billNo", billNo);
            cmd.Parameters.AddWithValue("$prefix", prefix);
            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$amount", (double)amount);
            cmd.Parameters.AddWithValue("$custCode", custCode);
            cmd.Parameters.AddWithValue("$customer", customer);
            cmd.Parameters.AddWithValue("$printStatus", printed ? "Yes" : "No");
            cmd.Parameters.AddWithValue("$tenderType", tenderType);
            cmd.Parameters.AddWithValue("$rrn", rrn);
            cmd.Parameters.AddWithValue("$isReturn", isReturn ? 1 : 0);
            billId = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        foreach (var it in items)
        {
            using var itemCmd = conn.CreateCommand();
            itemCmd.Transaction = tx;
            itemCmd.CommandText = @"
                INSERT INTO BillItems (BillId, Code, Item, Rate, Qty, DiscPct)
                VALUES ($billId, $code, $item, $rate, $qty, $discPct);";
            itemCmd.Parameters.AddWithValue("$billId", billId);
            itemCmd.Parameters.AddWithValue("$code", it.Code);
            itemCmd.Parameters.AddWithValue("$item", it.Item);
            itemCmd.Parameters.AddWithValue("$rate", (double)it.Rate);
            itemCmd.Parameters.AddWithValue("$qty", it.Qty);
            itemCmd.Parameters.AddWithValue("$discPct", (double)it.DiscPct);
            itemCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static int GetNextPurchaseGrnNo()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) + 1 FROM PurchaseInvoices;";
        var val = cmd.ExecuteScalar();
        return val != null ? Convert.ToInt32(val) : 1;
    }

    public static void SavePurchaseInvoice(
        string grnNo,
        string grnDate,
        string invoiceNo,
        string distributor,
        decimal amount,
        string narration,
        IEnumerable<(string Code, string Name, int Qty, int Free, decimal PurRate, decimal NetAmt, string Barcode, string Size, string HsnCode)> items)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        long purchaseId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO PurchaseInvoices (GrnNo, GrnDate, InvoiceNo, Distributor, Amount, Narration)
                VALUES ($grnNo, $grnDate, $invoiceNo, $distributor, $amount, $narration);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$grnNo", grnNo);
            cmd.Parameters.AddWithValue("$grnDate", grnDate);
            cmd.Parameters.AddWithValue("$invoiceNo", invoiceNo);
            cmd.Parameters.AddWithValue("$distributor", distributor);
            cmd.Parameters.AddWithValue("$amount", (double)amount);
            cmd.Parameters.AddWithValue("$narration", narration);
            purchaseId = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        foreach (var it in items)
        {
            using (var itemCmd = conn.CreateCommand())
            {
                itemCmd.Transaction = tx;
                itemCmd.CommandText = @"
                    INSERT INTO PurchaseItems (PurchaseId, Code, Name, Qty, Free, PurRate, NetAmt, Barcode, Size, HsnCode)
                    VALUES ($purchaseId, $code, $name, $qty, $free, $purRate, $netAmt, $barcode, $size, $hsnCode);";
                itemCmd.Parameters.AddWithValue("$purchaseId", purchaseId);
                itemCmd.Parameters.AddWithValue("$code", it.Code);
                itemCmd.Parameters.AddWithValue("$name", it.Name);
                itemCmd.Parameters.AddWithValue("$qty", it.Qty);
                itemCmd.Parameters.AddWithValue("$free", it.Free);
                itemCmd.Parameters.AddWithValue("$purRate", (double)it.PurRate);
                itemCmd.Parameters.AddWithValue("$netAmt", (double)it.NetAmt);
                itemCmd.Parameters.AddWithValue("$barcode", it.Barcode);
                itemCmd.Parameters.AddWithValue("$size", it.Size);
                itemCmd.Parameters.AddWithValue("$hsnCode", it.HsnCode);
                itemCmd.ExecuteNonQuery();
            }

            int addStock = Math.Max(0, it.Qty + it.Free);
            if (addStock > 0)
            {
                using var stockCmd = conn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "UPDATE Products SET Stock = Stock + $addQty WHERE Code = $code;";
                stockCmd.Parameters.AddWithValue("$addQty", addStock);
                stockCmd.Parameters.AddWithValue("$code", it.Code);
                stockCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public static List<(string GrnNo, string Date, string Distributor, decimal Amount)> GetRecentPurchases(int limit = 20)
    {
        EnsureInitialized();
        var list = new List<(string GrnNo, string Date, string Distributor, decimal Amount)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT GrnNo, GrnDate, Distributor, Amount
            FROM PurchaseInvoices
            ORDER BY Id DESC
            LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                Convert.ToDecimal(r.GetDouble(3))
            ));
        }
        return list;
    }

    public sealed class DistributorRecord
    {
        public int Sno { get; set; }
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Landmark { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public string PinCode { get; set; } = string.Empty;
        public string TelOffice { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public string DistType { get; set; } = "None";
        public string CurrencyName { get; set; } = "INR";
        public string TelRes { get; set; } = string.Empty;
        public string DistributorId { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string BarcodeAlias { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string PanNo { get; set; } = string.Empty;
        public string GstRegType { get; set; } = "Regular";
        public string OrderDays { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        public int CreditDays { get; set; }
    }

    public static string GetNextDistributorCode()
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) + 1 FROM Distributors;";
        var next = Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
        return $"D{next:D3}";
    }

    public static List<DistributorRecord> GetDistributors(string search = "")
    {
        EnsureInitialized();
        var list = new List<DistributorRecord>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        const string cols = @"
            Id, Code, Name, Address, Phone, GstNo, DistType, CreditDays,
            ShortName, Location, Landmark, City, State, Country, PinCode,
            TelOffice, Email, Fax, CurrencyName, TelRes, DistributorId,
            Area, BarcodeAlias, ContactPerson, PanNo, GstRegType, OrderDays, Comments";

        if (string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = $"SELECT {cols} FROM Distributors ORDER BY Name COLLATE NOCASE ASC, Id ASC;";
        }
        else
        {
            cmd.CommandText = $@"
                SELECT {cols}
                FROM Distributors
                WHERE Code LIKE $q OR Name LIKE $q OR ShortName LIKE $q OR Phone LIKE $q OR City LIKE $q OR Area LIKE $q OR GstNo LIKE $q
                ORDER BY Name COLLATE NOCASE ASC, Id ASC;";
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        using var r = cmd.ExecuteReader();
        int sno = 1;
        while (r.Read())
        {
            list.Add(new DistributorRecord
            {
                Sno = sno++,
                Id = r.GetInt32(0),
                Code = r.GetString(1),
                Name = r.GetString(2),
                Address = r.GetString(3),
                Phone = r.GetString(4),
                GstNo = r.GetString(5),
                DistType = r.GetString(6),
                CreditDays = r.GetInt32(7),
                ShortName = r.GetString(8),
                Location = r.GetString(9),
                Landmark = r.GetString(10),
                City = r.GetString(11),
                State = r.GetString(12),
                Country = r.GetString(13),
                PinCode = r.GetString(14),
                TelOffice = r.GetString(15),
                Email = r.GetString(16),
                Fax = r.GetString(17),
                CurrencyName = r.GetString(18),
                TelRes = r.GetString(19),
                DistributorId = r.GetString(20),
                Area = r.GetString(21),
                BarcodeAlias = r.GetString(22),
                ContactPerson = r.GetString(23),
                PanNo = r.GetString(24),
                GstRegType = r.GetString(25),
                OrderDays = r.GetString(26),
                Comments = r.GetString(27)
            });
        }
        return list;
    }

    public static void SaveDistributor(DistributorRecord d)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        if (d.Id <= 0)
        {
            cmd.CommandText = @"
                INSERT INTO Distributors (
                    Code, Name, Address, Phone, GstNo, DistType, CreditDays,
                    ShortName, Location, Landmark, City, State, Country, PinCode,
                    TelOffice, Email, Fax, CurrencyName, TelRes, DistributorId,
                    Area, BarcodeAlias, ContactPerson, PanNo, GstRegType, OrderDays, Comments
                ) VALUES (
                    $code, $name, $address, $phone, $gstNo, $distType, $creditDays,
                    $shortName, $location, $landmark, $city, $state, $country, $pinCode,
                    $telOffice, $email, $fax, $currencyName, $telRes, $distributorId,
                    $area, $barcodeAlias, $contactPerson, $panNo, $gstRegType, $orderDays, $comments
                );";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Distributors
                SET Code = $code,
                    Name = $name,
                    Address = $address,
                    Phone = $phone,
                    GstNo = $gstNo,
                    DistType = $distType,
                    CreditDays = $creditDays,
                    ShortName = $shortName,
                    Location = $location,
                    Landmark = $landmark,
                    City = $city,
                    State = $state,
                    Country = $country,
                    PinCode = $pinCode,
                    TelOffice = $telOffice,
                    Email = $email,
                    Fax = $fax,
                    CurrencyName = $currencyName,
                    TelRes = $telRes,
                    DistributorId = $distributorId,
                    Area = $area,
                    BarcodeAlias = $barcodeAlias,
                    ContactPerson = $contactPerson,
                    PanNo = $panNo,
                    GstRegType = $gstRegType,
                    OrderDays = $orderDays,
                    Comments = $comments
                WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", d.Id);
        }

        cmd.Parameters.AddWithValue("$code", d.Code.Trim());
        cmd.Parameters.AddWithValue("$name", d.Name.Trim());
        cmd.Parameters.AddWithValue("$address", d.Address.Trim());
        cmd.Parameters.AddWithValue("$phone", d.Phone.Trim());
        cmd.Parameters.AddWithValue("$gstNo", d.GstNo.Trim());
        cmd.Parameters.AddWithValue("$distType", string.IsNullOrWhiteSpace(d.DistType) ? "None" : d.DistType.Trim());
        cmd.Parameters.AddWithValue("$creditDays", Math.Max(0, d.CreditDays));
        cmd.Parameters.AddWithValue("$shortName", d.ShortName.Trim());
        cmd.Parameters.AddWithValue("$location", d.Location.Trim());
        cmd.Parameters.AddWithValue("$landmark", d.Landmark.Trim());
        cmd.Parameters.AddWithValue("$city", d.City.Trim());
        cmd.Parameters.AddWithValue("$state", d.State.Trim());
        cmd.Parameters.AddWithValue("$country", string.IsNullOrWhiteSpace(d.Country) ? "India" : d.Country.Trim());
        cmd.Parameters.AddWithValue("$pinCode", d.PinCode.Trim());
        cmd.Parameters.AddWithValue("$telOffice", d.TelOffice.Trim());
        cmd.Parameters.AddWithValue("$email", d.Email.Trim());
        cmd.Parameters.AddWithValue("$fax", d.Fax.Trim());
        cmd.Parameters.AddWithValue("$currencyName", string.IsNullOrWhiteSpace(d.CurrencyName) ? "INR" : d.CurrencyName.Trim());
        cmd.Parameters.AddWithValue("$telRes", d.TelRes.Trim());
        cmd.Parameters.AddWithValue("$distributorId", d.DistributorId.Trim());
        cmd.Parameters.AddWithValue("$area", d.Area.Trim());
        cmd.Parameters.AddWithValue("$barcodeAlias", d.BarcodeAlias.Trim());
        cmd.Parameters.AddWithValue("$contactPerson", d.ContactPerson.Trim());
        cmd.Parameters.AddWithValue("$panNo", d.PanNo.Trim());
        cmd.Parameters.AddWithValue("$gstRegType", string.IsNullOrWhiteSpace(d.GstRegType) ? "Regular" : d.GstRegType.Trim());
        cmd.Parameters.AddWithValue("$orderDays", d.OrderDays.Trim());
        cmd.Parameters.AddWithValue("$comments", d.Comments.Trim());
        cmd.ExecuteNonQuery();
    }

    public static void DeleteDistributor(int id)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Distributors WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
