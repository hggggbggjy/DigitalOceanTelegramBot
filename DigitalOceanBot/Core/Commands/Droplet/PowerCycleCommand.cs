﻿using System;
using System.Threading;
using System.Threading.Tasks;
using DigitalOcean.API;
using DigitalOceanBot.Core.Attributes;
using DigitalOceanBot.Extensions;
using DigitalOceanBot.Messages;
using DigitalOceanBot.Services;
using DigitalOceanBot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DigitalOceanBot.Core.Commands.Droplet
{
    [BotCommand(BotCommandType.DropletPowerCycle)]
    public sealed class PowerCycleCommand : IBotCommand
    {
        private readonly ITelegramBotClient _telegramBotClient;
        private readonly IDigitalOceanClient _digitalOceanClient;
        private readonly StorageService _storageService;

        public PowerCycleCommand(
            ITelegramBotClient telegramBotClient,
            IDigitalOceanClient digitalOceanClient,
            StorageService storageService)
        {
            _telegramBotClient = telegramBotClient;
            _digitalOceanClient = digitalOceanClient;
            _storageService = storageService;
        }

        public async Task ExecuteCommandAsync(Message message)
        {
            var dropletId = _storageService.Get<long>(StorageKeys.DropletId);

            if (dropletId > 0)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(3));
                var action = await _digitalOceanClient.DropletActions.PowerCycle(dropletId);
                var status = action.GetStatus();

                while (status is ActionStatus.Waiting)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    
                    var actionResult = await _digitalOceanClient.DropletActions.GetDropletAction(dropletId, action.Id);
                    status = actionResult.GetStatus();
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
                
                switch (status)
                {
                    case ActionStatus.Success:
                        await _telegramBotClient.SendTextMessageAsync(
                            chatId:message.Chat.Id, 
                            text:CommonMessage.GetDoneMessage());
                        break;
                    case ActionStatus.Error:
                        await _telegramBotClient.SendTextMessageAsync(
                            chatId:message.Chat.Id, 
                            text:CommonMessage.GetErrorMessage());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(status));
                }
            }
        }
    }
}
