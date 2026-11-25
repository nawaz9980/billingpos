using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuickPos.Services;

public class SavedBillItem
{
    public string Code { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Qty { get; set; } = 1;
    public int ReturnedQty { get; set; }
    public int RemainingQty => Math.Max(0, Qty - ReturnedQty);
    public decimal DiscPct { get; set; }
}

public class SavedBillRecord
{
    public int Sno { get; set; }
    private string _date = string.Empty;
    public string Date
    {
        get => _date;
        set
        {
            _date = value;
            BillDate = DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : DateTime.Today;
        }
    }
    public DateTime BillDate { get; private set; } = DateTime.Today;
    public string BillNo { get; set; } = string.Empty;
    public string Prefix { get; set; } = "WL";
    public decimal Amount { get; set; }
    public string CustCode { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string User { get; set; } = "SALES";
    public string Print { get; set; } = "Yes";
    public bool IsReturn { get; set; }
    public List<SavedBillItem> Items { get; set; } = [];
}

/// <summary>
/// SQLite-backed store for saved bills and returns (starts empty).
/// </summary>
public static class BillHistoryStore
{
    public static IReadOnlyList<SavedBillRecord> GetBills()
    {
        return DatabaseService.GetBills(50);
    }

    public static string AddBill(string billNo, decimal amount, string customer, bool printed, string tenderType, IEnumerable<SavedBillItem> items, string rrn = "", string custCode = "")
    {
        bool isReturn = tenderType == "RRN" || billNo.StartsWith("RTN", StringComparison.OrdinalIgnoreCase);
        string prefix = isReturn ? "RTN" : tenderType switch
        {
            "UPI" => "WL",
            "Debit Card" => "CC",
            "Credit Card" => "CC",
            _ => "CA"
        };

        int nextNo = isReturn
            ? DatabaseService.GetNextReturnBillNumber()
            : DatabaseService.GetNextSalesBillNumber();
        string generatedBillNo = $"{prefix}{nextNo}";

        DatabaseService.AddBill(
            generatedBillNo,
            prefix,
            DateTime.Now.ToString("dd/MM/yyyy"),
            amount,
            string.IsNullOrWhiteSpace(custCode) ? "1" : custCode,
            customer,
            printed,
            tenderType,
            rrn,
            isReturn,
            items);

        return generatedBillNo;
    }
}
