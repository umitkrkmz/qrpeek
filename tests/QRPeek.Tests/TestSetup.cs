using Xunit;

// S (every user-facing string) keeps the UI language in one static field, and
// test classes set it in their constructors: InvoiceTests to Turkish, the
// others to English. xUnit runs test classes in parallel by default, so one
// class could switch the language under another mid-test (seen once: the CSV
// header came out in English and Csv_uses_the_turkish_excel_format failed).
// The whole suite takes a few seconds, so run it one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
