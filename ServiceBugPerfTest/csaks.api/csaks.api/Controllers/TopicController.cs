/// <summary>
/// 
/// </summary>
namespace csaks.api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using csaks.api.Common;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Extensions.Logging;


    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TopicController : ControllerBase
    {
        private ILogger logger = null;
        const string ServiceBusConnectionString = "";
        const string TopicName = "";
        const string MySubName = "";

        const string SubscriptionName = "mysub";

        private static TestInput InputSetting { get; set; } = new TestInput { SingleSessionId = "single1", SingleSessionProcessCount = 100, PrefetchCount = 0, Sessions = new string[] { "multisession1", "multisession2", "multisession3", "multisession4", "multisession5", "multisession6" }, MultiProcessCount = 10 };

        TopicClient topicClient = new TopicClient(ServiceBusConnectionString, TopicName);
        SessionClient sessionClient = new SessionClient(ServiceBusConnectionString, MySubName, ReceiveMode.PeekLock, null, InputSetting.PrefetchCount);

        public TopicController(ILogger<TopicController> _logger)
        {
            this.logger = _logger;
        }


        [HttpGet]
        public async Task<JsonResult> KVTest(string keyName)
        {
            KeyVaultTest kvTest = new KeyVaultTest();
            //var result= await kvTest.GetAccessTokenAsync();
            var result = await kvTest.OnGetAsync(keyName);

            return new JsonResult($"KeyName - {keyName} Get value - {result}");
        }

        /// <summary>
        /// get context opertion configurations
        /// </summary>
        /// <param name="testChanges"></param>
        /// <returns></returns>
        [HttpGet]
        public JsonResult GetSessionConfig(int testChangesV2)
        {
            return new JsonResult(InputSetting);
        }

        /// <summary>
        /// set context opertion configurations
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonResult SetSessionConfig(TestInput input)
        {
            var result = new List<string>();
            try
            {
                if (input.PrefetchCount >= 0 && InputSetting.PrefetchCount != input.PrefetchCount)
                {
                    sessionClient = new SessionClient(ServiceBusConnectionString, MySubName, ReceiveMode.PeekLock, null, InputSetting.PrefetchCount);
                }
                InputSetting = input;

                result.Add($"send success!");
            }
            catch (Exception ex)
            {
                result.Add($"send fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// send message one by one
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> SendSingleTest(int totalCount = 0)
        {
            var result = new List<string>();
            try
            {
                string sessionid = InputSetting.SingleSessionId;
                var sw = Stopwatch.StartNew();
                totalCount = totalCount == 0 ? InputSetting.SingleSessionProcessCount : totalCount;

                for (int i = 0; i < totalCount; i++)
                {
                    Message message = new Message(Encoding.UTF8.GetBytes($"No {i} Message - test"))
                    {
                        SessionId = sessionid
                    };
                    await topicClient.SendAsync(message);
                }
                result.Add($"send success!");
                result.Add($"time cost - {sw.ElapsedMilliseconds}, message count - {totalCount}");
            }
            catch (Exception ex)
            {
                result.Add($"send fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// send message by list
        /// </summary>
        /// <param name="sendCount"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> SendSingleListTest(int sendCount = 0)
        {
            sendCount = sendCount == 0 ? InputSetting.SingleSessionProcessCount : sendCount;
            var result = new List<string>();
            try
            {
                List<Message> msgList = new List<Message>();
                string sessionid = InputSetting.SingleSessionId;
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < sendCount; i++)
                {
                    Message message = new Message(Encoding.UTF8.GetBytes($"No {i} Message - test"))
                    {
                        SessionId = sessionid
                    };
                    msgList.Add(message);
                }
                await topicClient.SendAsync(msgList);
                result.Add($"send success!");
                result.Add($"time cost - {sw.ElapsedMilliseconds}, message count - {sendCount}");
            }
            catch (Exception ex)
            {
                result.Add($"send fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// receive message one by one in multi-threads
        /// </summary>
        /// <param name="totalCount"></param>
        /// <param name="multiThreadCount"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> MTReceiveTest(int totalCount = 0, int multiThreadCount = 1)
        {
            var result = new List<string>();
            totalCount = totalCount == 0 ? InputSetting.SingleSessionProcessCount : totalCount;
            string sessionid = InputSetting.SingleSessionId;
            int perCount = totalCount / multiThreadCount;
            var sw = Stopwatch.StartNew();

            try
            {
                var session = await sessionClient.AcceptMessageSessionAsync(sessionid);
                List<Task> tasks = new List<Task>();

                for (int m = 0; m < multiThreadCount; m++)
                {
                    tasks.Add(ReveiveMessageTask(perCount, session));
                }

                await Task.WhenAll(tasks).ContinueWith(async closeTask => { await sessionClient.CloseAsync(); });

                var endSec = sw.ElapsedMilliseconds;
                result.Add($"receive success!");
                result.Add($"get session {InputSetting.SingleSessionId} end - time cost - {endSec}, messageCount - {totalCount}");
            }
            catch (Exception ex)
            {
                result.Add($"receive fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        private async Task ReveiveMessageTask(int perCount, IMessageSession session)
        {
            for (int i = 0; i < perCount; i++)
            {
                await session.ReceiveAsync().ContinueWith(async task =>
                 {
                     await session.CompleteAsync(task.Result.SystemProperties.LockToken);
                 });
            }
        }

        /// <summary>
        /// receive message by list in multi-threads
        /// </summary>
        /// <param name="totalCount"></param>
        /// <param name="multiThreadCount"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> MTReceiveListTest(int totalCount = 0, int multiThreadCount = 1)
        {
            var result = new List<string>();
            totalCount = totalCount == 0 ? InputSetting.SingleSessionProcessCount : totalCount;
            var sw = Stopwatch.StartNew();

            try
            {
                string sessionid = InputSetting.SingleSessionId;
                int perCount = totalCount / multiThreadCount;
                var session = await sessionClient.AcceptMessageSessionAsync(sessionid);

                List<Task> tasks = new List<Task>();
                for (int m = 0; m < multiThreadCount; m++)
                {
                    tasks.Add(ReveiveListMessageTask(perCount, session));
                }

                await Task.WhenAll(tasks).ContinueWith(async closeTask => { await sessionClient.CloseAsync(); });

                var endSec = sw.ElapsedMilliseconds;
                result.Add("receive success!");
                result.Add($"get session {InputSetting.SingleSessionId} end - time cost - {endSec}, messageCount - {totalCount}");
            }
            catch (Exception ex)
            {
                result.Add($"receive fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        private async Task ReveiveListMessageTask(int perCount, IMessageSession session)
        {
            int leftMsgCount = perCount;
            while (leftMsgCount > 0)
            {
                var result = await session.ReceiveAsync(leftMsgCount).ContinueWith(async task =>
                {
                    leftMsgCount = leftMsgCount - task.Result.Count;
                    List<string> tokens = task.Result.Select(msg => msg.SystemProperties.LockToken).ToList();

                    await session.CompleteAsync(tokens);
                });
            }
        }

        /// <summary>
        /// receive message one by one
        /// </summary>
        /// <param name="totalCount"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> ReceiveTest(int totalCount = 0)
        {
            var result = new List<string>();
            totalCount = totalCount == 0 ? InputSetting.SingleSessionProcessCount : totalCount;

            try
            {
                string sessionid = InputSetting.SingleSessionId;
                var sw = Stopwatch.StartNew();

                var session = await sessionClient.AcceptMessageSessionAsync(sessionid);
                var saSec = sw.ElapsedMilliseconds;
                result.Add($"get session {sessionid} start - time cost - {saSec}");

                await ReveiveMessageTask(totalCount, session).ContinueWith(async task => { await sessionClient.CloseAsync(); });

                var endSec = sw.ElapsedMilliseconds;
                result.Add($"receive success!");
                result.Add($"get message {sessionid} end - time cost - {endSec}, messageCount - {InputSetting.SingleSessionProcessCount}");
            }
            catch (Exception ex)
            {
                result.Add($"receive fail!");
                result.Add(ex.Message);
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// receive message by list
        /// </summary>
        /// <param name="totalCount"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> ReceiveListTest(int totalCount)
        {
            var result = new List<string>();
            try
            {
                string sessionid = InputSetting.SingleSessionId;
                var sw = Stopwatch.StartNew();

                var session = await sessionClient.AcceptMessageSessionAsync(sessionid);
                var saSec = sw.ElapsedMilliseconds;
                result.Add($"get session {sessionid} start - time cost - {saSec}");

                await ReveiveListMessageTask(totalCount, session).ContinueWith(async task => { await sessionClient.CloseAsync(); });

                var endSec = sw.ElapsedMilliseconds;
                result.Add("receive success!");
                result.Add($"get session {sessionid} end - time cost - {endSec}, messageCount - {totalCount}");
            }
            catch (Exception ex)
            {
                result.Add($"receive fail!");
                result.Add(ex.Message);
            }
            finally
            {
                await sessionClient.CloseAsync();
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// send message to multi sessions
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> SendMultiSessionTest()
        {
            var result = "send false ";
            try
            {
                foreach (var sid in InputSetting.Sessions)
                {
                    for (int i = 0; i < InputSetting.MultiProcessCount; i++)
                    {
                        Message message = new Message(Encoding.UTF8.GetBytes($"No {i} Message - test"))
                        {
                            SessionId = sid
                        };
                        await topicClient.SendAsync(message);
                    }
                }
                result = "send success!";
            }
            catch (Exception ex)
            {
                result += ex.Message;
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// receive message from multi sessions
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> ReceiveMultiSessionTest()
        {
            var result = "receive false ";
            var sw = Stopwatch.StartNew();
            string diagText = "";

            try
            {
                foreach (var sid in InputSetting.Sessions)
                {
                    var session = await sessionClient.AcceptMessageSessionAsync(sid);
                    var saSec = sw.ElapsedMilliseconds;
                    diagText += $"get session {sid} start - time cost - {saSec}  ";
                    for (int i = 0; i < InputSetting.MultiProcessCount; i++)
                    {
                        var m = await session.ReceiveAsync();
                        session.CompleteAsync(m.SystemProperties.LockToken).Wait();
                    }
                    var endSec = sw.ElapsedMilliseconds;
                    diagText += $"get session {sid} end - time cost - {endSec}, messageCount - {InputSetting.MultiProcessCount}  ";
                }

                result = "receive success!" + diagText;
            }
            catch (Exception ex)
            {
                result += ex.Message;
            }

            return new JsonResult(result);
        }
    }

    public class TestInput
    {
        public string SingleSessionId { get; set; }
        public int PrefetchCount { get; set; }
        public int SingleSessionProcessCount { get; set; }
        public string[] Sessions { get; set; }
        public int MultiProcessCount { get; set; }
    }
}
