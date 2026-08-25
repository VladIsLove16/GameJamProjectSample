# Jam Starter

Нейтральная стартовая основа для короткого джема на Unity 6. Она не содержит
жанровой логики: сцену `Sandbox` можно сразу заменить прототипом конкретной игры.

## Быстрый старт

1. Откройте проект и выберите `Jam Starter > Play From Bootstrap`.
2. Для разработки игры меняйте или копируйте `Scenes/Sandbox.unity`.
3. Сохраните `Bootstrap` первой сценой в Build Settings: она создаёт сервисы и
   загружает главное меню.
4. Перед сдачей выполните `Jam Starter > Validate Project`, затем
   `Jam Starter > Quick Build Active Target`.

`Generate or Rebuild Starter` предназначена для восстановления исходного шаблона.
Она пересоздаёт три сцены и Audio Mixer, поэтому не запускайте её после того, как
начали изменять сгенерированные сцены или настройки микшера. Runtime-код команда
не перезаписывает, а существующий Input Actions asset сохраняет.

## Архитектура

- `Bootstrap` содержит постоянный `AppBootstrap` — composition root приложения.
- Сценовый компонент реализует `IAppServicesConsumer` и получает `AppServices`
  через `Initialize`. Глобального service locator нет.
- `SceneLoader` выполняет одиночную асинхронную загрузку, блокирует повторный
  запрос и использует fade, независимый от `Time.timeScale`.
- `GamePauseService` централизованно управляет паузой и восстанавливает прежний
  масштаб времени.
- `CountdownTimer`, `SeededRandom` и `ComponentPool<T>` не зависят от конкретной
  игры и готовы для игровой логики.

## Ввод

`InputReader` предоставляет состояния и события, не раскрывая Input System
сценовой логике. Он клонирует action asset во время запуска, поэтому переключение
карт не затрагивает `EventSystem`.

- `Gameplay`: Move, Look, Primary, Secondary, Interact, Pause.
- `UI`: Navigate, Submit, Cancel, Point, Click, ScrollWheel.
- Включены клавиатура/мышь, gamepad, touch и pen там, где это применимо.
- Переключение: `services.Input.UseGameplay()`, `UseUI()` или `DisableInput()`.

Привязки редактируются в `Settings/JamInputActions.inputactions`. Имена карт и
действий являются контрактом `InputReader`; при их изменении обновите reader.

## UI

Главное меню, пауза, настройки и экран результата уже связаны через сериализованные
ссылки. Canvas использует `CanvasScaler`, а корневой контейнер — `SafeAreaFitter`,
поэтому интерфейс адаптируется к разрешению и вырезам мобильных экранов.

Для нового экрана наследуйте/используйте `UIScreen`, создавайте объекты в сцене и
передавайте их контроллеру через `[SerializeField]`. Не ищите UI по строковым именам.

## Audio

`JamAudioMixer` содержит группы `Master/Music/SFX/UI` и параметры
`MasterVolume`, `MusicVolume`, `SfxVolume`, `UiVolume`. `SettingsService` хранит
громкость, качество и fullscreen в одном версионированном JSON.

- Музыка: `services.Audio.PlayMusic(clip)` и `StopMusic()`.
- Эффекты: `PlaySfx(cue)` или `PlaySfxAt(clip, position)`.
- Интерфейс: `PlayUi(cue)`.
- `AudioCue` поддерживает несколько клипов, диапазон громкости/тона и защиту от
  немедленного повтора. Одноразовые AudioSource берутся из ограниченного пула.

## Инструменты редактора

- `Play From Bootstrap` — запуск из правильной точки независимо от открытой сцены.
- `Stop Using Bootstrap` — вернуть обычный запуск открытой сцены.
- `Configure Build Scenes` — восстановить порядок Bootstrap/MainMenu/Sandbox.
- `Validate Project` — проверить сцены, missing scripts и обязательные ссылки.
- `Clear Saved Settings` — удалить только настройки Jam Starter из PlayerPrefs.
- `Quick Build Active Target` — проверить конфигурацию и собрать активную платформу
  в `Builds/<BuildTarget>`.

EditMode-тесты покрывают таймер и детерминированный random, PlayMode-тесты —
bootstrap-переход и паузу. Тестовые assembly definitions изолированы от билда.

## Осознанно не включено

В шаблоне нет жанрового gameplay, сохранения прогресса, Addressables, локализации,
аналитики и DI-фреймворка. На коротком джеме эти системы следует добавлять только
когда они действительно нужны концепту игры.
