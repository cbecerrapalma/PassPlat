using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;

class Program
{
    static async Task Main(string[] args)
    {
        var jwt = args[1];
        var url = "http://localhost:5259/api/dispconfiables/trigger-new-ip/3?ip=203.0.113.57";

        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        client.Timeout = Timeout.InfiniteTimeSpan;

        Console.WriteLine($"=== CONCURRENCE TEST START: {DateTime.UtcNow} ===");
        Console.WriteLine($"URL: {url}");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Start both requests at the same time using Task.WhenAll
        var tcsA = new TaskCompletionSource<HttpResponseMessage>();
        var tcsB = new TaskCompletionSource<HttpResponseMessage>();

        // Use a barrier to start both requests simultaneously
        var barrier = new Barrier(2);

        var taskA = Task.Run(() => {
            barrier.SignalAndWait();
            return client.GetAsync(url);
        }).Unwrap();

        var taskB = Task.Run(() => {
            barrier.SignalAndWait();
            return client.GetAsync(url);
        }).Unwrap();

        await Task.WhenAll(taskA, taskB);

        string responseA, responseB;
        int statusA, statusB;
        string errorA = null, errorB = null;

        try {
            responseA = await taskA.Result.Content.ReadAsStringAsync();
            statusA = (int)taskA.Result.StatusCode;
        } catch (Exception ex) {
            responseA = ex.Message;
            statusA = -1;
            errorA = ex.Message;
            Console.WriteLine($"Request A Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null) {
                Console.WriteLine($"Request A Inner Exception Type: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Request A Inner Exception Message: {ex.InnerException.Message}");
            }
        }

        try {
            responseB = await taskB.Result.Content.ReadAsStringAsync();
            statusB = (int)taskB.Result.StatusCode;
        } catch (Exception ex) {
            responseB = ex.Message;
            statusB = -1;
            errorB = ex.Message;
            Console.WriteLine($"Request B Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null) {
                Console.WriteLine($"Request B Inner Exception Type: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Request B Inner Exception Message: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine($"=== RESULT ===");
        Console.WriteLine($"Request A: Status={statusA}, Response={responseA}");
        if (errorA != null) Console.WriteLine($"  Exception: {errorA}");
        Console.WriteLine($"Request B: Status={statusB}, Response={responseB}");
        if (errorB != null) Console.WriteLine($"  Exception: {errorB}");
    }
}
