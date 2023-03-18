using System;
using System.Collections.Generic;
using System.Threading;

namespace EventThrottlingBus
{
    public class EventThrottlingBus
    {
        // Колекція делегатів для подій
        private readonly Dictionary<string, List<Action<object>>> _eventHandlers;

        // Колекція черг подій
        private readonly Dictionary<string, Queue<object>> _eventQueues;

        // Колекція таймерів
        private readonly Dictionary<string, Timer> _eventTimers;

        // Інтервал обмеження для таймерів
        private readonly int _throttleInterval = 1000;

        public EventThrottlingBus()
        {
            _eventHandlers = new Dictionary<string, List<Action<object>>>();
            _eventQueues = new Dictionary<string, Queue<object>>();
            _eventTimers = new Dictionary<string, Timer>();
        }

        // Метод для реєстрації обробника події
        public void Subscribe(string eventName, Action<object> handler)
        {
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName] = new List<Action<object>>();
                _eventQueues[eventName] = new Queue<object>();
            }

            _eventHandlers[eventName].Add(handler);

            if (!_eventTimers.ContainsKey(eventName))
            {
                // Якщо для даної події ще не створено таймер, створюємо його та встановлюємо інтервал
                _eventTimers[eventName] = new Timer(OnEventTimerTick, eventName, _throttleInterval, Timeout.Infinite);
            }
        }

        // Метод для скасування реєстрації обробника події
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            if (_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName].Remove(handler);

                if (_eventHandlers[eventName].Count == 0)
                {
                    // Якщо для даної події більше не залишилося обробників, зупиняємо таймер та видаляємо зі списку таймерів та черг подій
                    _eventTimers[eventName].Dispose();
                    _eventTimers.Remove(eventName);
                    _eventQueues.Remove(eventName);
                }
            }
        }

        // Метод для публікації події
        public void Publish(string eventName, object data)
        {
            if (_eventHandlers.ContainsKey(eventName))
            {
                // Якщо для даної події існують обробники, додаємо дані до черги подій
                _eventQueues[eventName].Enqueue(data);
            }
        }

        // Метод, який викликається таймером при обробці подій
        private void OnEventTimerTick(object state)
        {
            string eventName = (string)state;

            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventTimers[eventName].Dispose();
                _eventTimers.Remove(eventName);
                _eventQueues.Remove(eventName);
                return;
            }
            if (_eventQueues[eventName].Count == 0)
            {
                // Якщо черга подій для даної події порожня зупиняємо таймер та виходимо
                _eventTimers[eventName].Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            // Отримуємо наступну подію з черги та відправляємо її до всіх обробників
            object data = _eventQueues[eventName].Dequeue();
            foreach (Action<object> handler in _eventHandlers[eventName])
            {
                handler(data);
            }

            // Перезапускаємо таймер з інтервалом обмеження
            _eventTimers[eventName].Change(_throttleInterval, Timeout.Infinite);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Створюємо об'єкт шини подій
            EventThrottlingBus eventBus = new EventThrottlingBus();

            // Реєструємо обробник події "message" з інтервалом обмеження в 1 секунду
            eventBus.Subscribe("message", (data) =>
            {
                Console.WriteLine($"Message received: {data}");
            });

            // Публікуємо кілька подій з задержкою, меншою за інтервал обмеження
            eventBus.Publish("message", "Hello");
            Thread.Sleep(500);
            eventBus.Publish("message", "World");
            Thread.Sleep(500);
            eventBus.Publish("message", "!");

            // Зупиняємо програму для перегляду результатів
            Console.ReadLine();
        }
    }
}