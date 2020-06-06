using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace ZonaFilm
{
    public class GetFilmsAndSerialZona
    {
        /// <summary>
        /// Делегат вызова
        /// используется длявызова метода при каждом новом найденном фильме или сериале
        /// </summary>
        public delegate void SearchNewElemEvent(InfoFideoOrSerial fideoOrSerial);

        /// <summary>
        /// Результаты поиска
        /// Двухмерный массив, первоя клетка отвечает за старницу, вторая за найденный фильм или сериал
        /// </summary>
        public InfoFideoOrSerial[][] FilmsAndSerials { get; private set; }
        /// <summary>
        /// Кол-во найденных страниц совпадений
        /// </summary>
        public uint CountPages { get; private set; }
        /// <summary>
        /// Кол-во совподающих результатов
        /// </summary>
        public uint CountResul { get; private set; }

        /// <summary>
        /// Строка запроса (необходима в дальнейшем если есть несколько страниц с результатами)
        /// </summary>
        private readonly string SearchUrl;
        /// <summary>
        /// WebClient для запросов результата
        /// </summary>
        private WebClient webClientSearch = new WebClient();

        /// <summary>
        /// Универсальный инцилизатор класса
        /// </summary>
        private void Start()
        {
            webClientSearch.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            webClientSearch.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:76.0) Gecko/20100101 Firefox/76.0");
            webClientSearch.Headers.Add("X-Requested-With", "XMLHttpRequest");
            webClientSearch.Encoding = Encoding.UTF8;
        }
        /// <summary>
        /// Поиск фильма или сериала по названию
        /// </summary>
        /// <param name="Search"></param>
        /// <param name="GetMoreInfo"></param>
        /// <param name="AllPages"></param>
        /// <param name="searchNewElemEvent"></param>
        public GetFilmsAndSerialZona(string Search, bool GetMoreInfo = false, bool AllPages = false, SearchNewElemEvent searchNewElemEvent = null)
        {
            Start();
            // Производим запрос на сервер
            string JsonReqest;
            SearchUrl = "https://w84.zona.plus/search/" + Search;
            JsonReqest = webClientSearch.DownloadString(SearchUrl);
            GetInfoHtml(JsonReqest);
            HtmlGetResulSearch(JsonReqest, GetMoreInfo, searchNewElemEvent);
            if (AllPages)
                for (uint i = 1; i < CountPages; i++)
                    LoadPages(i, GetMoreInfo, searchNewElemEvent);
        }
        public GetFilmsAndSerialZona(Filters Search, bool GetMoreInfo = false, bool AllPages = false, SearchNewElemEvent searchNewElemEvent = null)
        {
            Start();
            // Производим запрос на сервер
            string JsonReqest;
            SearchUrl = "https://w84.zona.plus/" +
                (Search.IsSerial ? "tvseries" : "movies") +
                (Search.GenreSelected != "0" ? "/" + Search.GenreSelected : null) +
                (Search.YearSelected != "0" ? "/" + Search.YearSelected : null) +
                (Search.CountrySelected != "0" ? "/" + Search.CountrySelected : null) +
                (Search.RatingSelected != "0" ? "/" + Search.RatingSelected : null) +
                (Search.SortSelected != "0" ? "/" + Search.SortSelected : null)
                ;
            JsonReqest = webClientSearch.DownloadString(SearchUrl);
            GetInfoHtml(JsonReqest);
            HtmlGetResulSearch(JsonReqest, GetMoreInfo, searchNewElemEvent);
            if (AllPages)
                for (uint i = 1; i < CountPages; i++)
                    LoadPages(i, GetMoreInfo, searchNewElemEvent);
        }

        /// <summary>
        /// Подгрузить страницу
        /// </summary>
        /// <param name="Pages">Номер страницы (отсчёт с 0)</param>
        /// <param name="searchNewElemEvent">Вызывается при каждом новом найденном фильме или сериале</param>
        public void LoadPages(uint Pages, bool GetMoreInfo = false, SearchNewElemEvent searchNewElemEvent = null)
        {
            if (Pages > CountPages) Pages = CountPages;
            if (FilmsAndSerials[Pages] != null) return;
            string JsonReqest = webClientSearch.DownloadString(SearchUrl + "?page=" + (Pages + 1));
            HtmlGetResulSearch(JsonReqest, GetMoreInfo, searchNewElemEvent);
        }
        /// <summary>
        /// Получить онформацию (о CountPages и CountResul) с Json ответа запроса
        /// </summary>
        /// <param name="Html"></param>
        private void GetInfoHtml(string Html)
        {
            // Получаем кол-во страниц
            string res = ParsJson(Html, "total_pages");
            if (res != null) CountPages = Convert.ToUInt32(res);
            // Получаем кол во результатов
            res = ParsJson(Html, "total_items");
            if (res != null) CountResul = Convert.ToUInt32(res);

            FilmsAndSerials = new InfoFideoOrSerial[CountPages][];
        }

        /// <summary>
        /// Обрабатывает json ответ с запроса поиска
        /// </summary>
        /// <param name="Html"></param>
        /// <param name="searchNewElemEvent"></param>
        private void HtmlGetResulSearch(string Html, bool GetMoreInfo, SearchNewElemEvent searchNewElemEvent = null)
        {
            List<InfoFideoOrSerial> dataFilms = new List<InfoFideoOrSerial>(0);

            string Page = ParsJson(Html, "current_page");
            uint PageUint = 0;
            if (Page != null) PageUint = Convert.ToUInt32(Page) - 1;

            // Очистка начала
            Html = Html.Remove(0, Html.IndexOf("\"items\""));
            Html = Html.Remove(0, Html.IndexOf('[') + 1);
            // Очистка конца
            Html = Html.Substring(0, Html.IndexOf(']'));
            // Разбиваем данные на части
            string[] Elements = Html.Split('}');
            Html = null;

            foreach (var elem in Elements)
            {
                string elemN = elem + "}";
                if (ParsJson(elemN, "name_rus") != null)
                {
                    List<InfoFideoOrSerial.DataVideo[]> Series = new List<InfoFideoOrSerial.DataVideo[]>(0);
                    string id = ParsJson(elemN, "mobi_link_id");
                    if (id == null) id = GetID(ParsJson(elemN, "name_id"));
                    dataFilms.Add(
                        new InfoFideoOrSerial(new InfoFideoOrSerial.DataVideo[][] { new InfoFideoOrSerial.DataVideo[] { new InfoFideoOrSerial.DataVideo(id) } }, ParsJson(elemN, "year"),
                        Convert.ToDouble(ParsJson(elemN, "rating_kinopoisk")?.Replace('.', ',')),
                        ParsJson(elemN, "name_rus"),
                        Convert.ToBoolean(ParsJson(elemN, "serial")),
                         ParsJson(elemN, "name_id"),
                         ParsJson(elemN, "cover")?.Replace("\\/", "/")
                        ));
                    if (GetMoreInfo) dataFilms[dataFilms.Count - 1].GetMoreInfo();
                    if (searchNewElemEvent != null)
                    {
                        try
                        {
                            searchNewElemEvent.Invoke(dataFilms[dataFilms.Count - 1]);
                        }
                        catch { }
                    }
                }
            }
            FilmsAndSerials[PageUint] = dataFilms.ToArray();
        }
        /// <summary>
        /// Получение Id видео или сериала по его id_name
        /// </summary>
        /// <param name="NameIdFilmOrSerial"></param>
        /// <returns></returns>
        private static string GetID(string NameIdFilmOrSerial)
        {
            string Html = (new WebClient()).DownloadString("https://w84.zona.plus/movies/" + NameIdFilmOrSerial);
            Html = Html.Remove(0, Html.IndexOf("entity-default-btn trailer-btn js-trailer"));
            Html = Html.Remove(0, Html.IndexOf("data-link"));
            Html = Html.Remove(0, Html.IndexOf('"') + 1);
            Html = Html.Substring(0, Html.IndexOf('"'));
            Html = Html.Remove(Html.LastIndexOf('/'), Html.Length - Html.LastIndexOf('/'));
            Html = Html.Substring(Html.LastIndexOf('/') + 1, Html.Length - Html.LastIndexOf('/') - 1);
            return Html;
        }

        /// <summary>
        /// Информация о фильме или сериале
        /// </summary>
        public class InfoFideoOrSerial
        {
            /// <summary>
            /// Делался ли запрос на Videos
            /// </summary>
            private bool ActivatedReqestVideos = false;
            /// <summary>
            /// Делался ли на запрос к трийлеру или к получению детальной информации
            /// </summary>
            private bool ActivatedReqestTrillerAndInfo = false;

            private DataVideo[][] videos;
            /// <summary>
            /// Части сериала (двухмерный массив 1 измерение сезон, второе сериал) или идин обьект с информацией о фильме
            /// </summary>
            public DataVideo[][] Videos
            {
                get
                {
                    if (ActivatedReqestVideos)
                    {
                        return videos;
                    }
                    else
                    {
                        GetVideos();
                        return videos;
                    }
                }
                private set
                {
                    videos = value;
                }

            }

            //======================================================= Информация получаемая при создании обьекта
            /// <summary>
            /// Год выхода
            /// </summary>
            public string Year { get; private set; }
            /// <summary>
            /// Рейтинг на кинопоиск
            /// </summary>
            public double Rating { get; private set; }
            /// <summary>
            /// Название фильма или сериала на русском
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// Если истина то сериал если ложь то фильм
            /// </summary>
            public bool IsSerial { get; private set; }
            /// <summary>
            /// Именой индификатор вайла
            /// </summary>
            public string Name_id { get; private set; }
            /// <summary>
            /// Ссылка на фото фильма или сериала
            /// </summary>
            public string UrlImg { get; private set; }
            //======================================================= Информация получаемая из сети
            private string[] triller;
            /// <summary>
            /// Жанр фильма или сериала
            /// </summary>
            public string[] Triller
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return triller;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return triller;
                    }
                }
                private set
                {
                    triller = value;
                }

            }

            private string[] genre;
            /// <summary>
            /// Жанр фильма или сериала
            /// </summary>
            public string[] Genre
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return genre;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return genre;
                    }
                }
                private set
                {
                    genre = value;
                }

            }

            private string[] country;
            /// <summary>
            /// Страна производстава фильма или сериала
            /// </summary>
            public string[] Country
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return country;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return country;
                    }
                }
                private set
                {
                    country = value;
                }
            }

            private string[] producer;
            /// <summary>
            /// Режиссёр фильма или сериала 
            /// </summary>
            public string[] Producer
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return producer;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return producer;
                    }
                }
                private set
                {
                    producer = value;
                }
            }

            private string[] scenario;
            /// <summary>
            /// Сценарий фильма или сериала 
            /// </summary>
            public string[] Scenario
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return scenario;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return scenario;
                    }
                }
                private set
                {
                    scenario = value;
                }
            }

            private string[] actor;
            /// <summary>
            /// Актёры фильма или сериала 
            /// </summary>
            public string[] Actor
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return actor;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return actor;
                    }
                }
                private set
                {
                    actor = value;
                }
            }

            private string time;
            /// <summary>
            /// Время фильма или сериала 
            /// </summary>
            public string Time
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return time;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return time;
                    }
                }
                private set
                {
                    time = value;
                }
            }

            private string[] premiere;
            /// <summary>
            /// Премьера фильма или сериала 
            /// </summary>
            public string[] Premiere
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return premiere;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return premiere;
                    }
                }
                private set
                {
                    premiere = value;
                }
            }

            private string info;
            /// <summary>
            /// Информация о фильме или сериале 
            /// </summary>
            public string Info
            {
                get
                {
                    if (ActivatedReqestTrillerAndInfo)
                    {
                        return info;
                    }
                    else
                    {
                        GetTrillerAndInfo();
                        return info;
                    }
                }
                private set
                {
                    info = value;
                }
            }

            public InfoFideoOrSerial(DataVideo[][] videos, string year, double rating, string name, bool IsSerial, string name_id, string UrlImg)
            {
                this.videos = videos;
                this.Year = year;
                this.Rating = rating;
                this.Name = name;
                this.IsSerial = IsSerial;
                this.Name_id = name_id;
                this.UrlImg = UrlImg;
            }
            public override string ToString()
            {
                return string.Format("{0}, Название: {1}, Год: {2}, Рейтинг: {3}", IsSerial ? "Сериал" : "Фильм", Name, Year, Rating);
            }

            //================================================================================================ Всё для получение всех видео
            /// <summary>
            /// Части сериала (двухмерный массив 1 измерение сезон, второе сериал) или идин обьект с информацией о фильме
            /// </summary>
            /// <returns></returns>
            private void GetVideos()
            {
                if (IsSerial)
                {
                    string[] UrlSeason = GetUrlSeason(Name_id);
                    List<DataVideo[]> seasonVid = new List<DataVideo[]>(0);
                    if (UrlSeason?.Length <= 0) UrlSeason = new string[] { "https://w84.zona.plus/tvseries/" + Name_id };

                    foreach (var season in UrlSeason)
                    {
                        List<DataVideo> series = new List<DataVideo>(0);
                        string[] ids = GetIdSeason(season);
                        if (ids?.Length <= 0)
                        {
                            string Res = (new WebClient()).DownloadString("https://w84.zona.plus/tvseries/" + Name_id);
                            Res = Res.Remove(0, Res.IndexOf("entity-default-btn entity-play-btn"));
                            Res = Res.Remove(0, Res.IndexOf("data-id"));
                            Res = Res.Remove(0, Res.IndexOf('"') + 1);
                            Res = Res.Substring(0, Res.IndexOf('"'));
                            ids = new string[] { Res };
                        }
                        foreach (var id in ids)
                        {
                            series.Add(new DataVideo(id));
                        }
                        seasonVid.Add(series.ToArray());
                    }
                    videos = seasonVid.ToArray();
                }
                ActivatedReqestVideos = true;
            }
            /// <summary>
            /// Получение списка сезонов у сериала
            /// </summary>
            /// <param name="data">Один из обьектов видео сериала</param>
            /// <returns></returns>
            private static string[] GetUrlSeason(string name_id)
            {
                List<string> resul = new List<string>(0);
                string Html = (new WebClient()).DownloadString("https://w84.zona.plus/tvseries/" + name_id);
                if (Html.Contains("entity-seasons"))
                {
                    Html = Html.Remove(0, Html.LastIndexOf("entity-seasons"));
                    Html = Html.Substring(0, Html.IndexOf("</div>"));
                    while (Html.Contains("entity-season js-entity-season"))
                    {
                        string ElemRes = Html.Substring(0, Html.IndexOf("/a>"));
                        Html = Html.Remove(0, Html.IndexOf("/a>") + 3);
                        ElemRes = ElemRes.Remove(0, ElemRes.IndexOf("href"));
                        ElemRes = ElemRes.Remove(0, ElemRes.IndexOf('"') + 1);
                        ElemRes = ElemRes.Substring(0, ElemRes.IndexOf('"'));
                        resul.Add("https://w84.zona.plus" + ElemRes);
                    }
                }
                return resul.ToArray();
            }
            /// <summary>
            /// Получение id серий в сезоне
            /// </summary>
            /// <param name="Url"></param>
            /// <returns></returns>
            private static string[] GetIdSeason(string Url)
            {
                List<string> resul = new List<string>(0);
                string Html = (new WebClient()).DownloadString(Url);
                if (Html.Contains("items episodes is-entity-page js-kinetic-inner js-episodes"))
                {
                    Html = Html.Remove(0, Html.IndexOf("items episodes is-entity-page js-kinetic-inner js-episodes"));
                    Html = Html.Substring(0, Html.LastIndexOf("</ul>"));
                    while (Html.Contains("data-id"))
                    {
                        string elem = Html.Substring(0, Html.IndexOf("</li>") + 5);
                        Html = Html.Remove(0, Html.IndexOf("</li>") + 5);
                        elem = elem.Remove(0, elem.IndexOf("data-id"));
                        elem = elem.Remove(0, elem.IndexOf('"') + 1);
                        elem = elem.Substring(0, elem.IndexOf('"'));
                        resul.Add(elem);
                    }
                }
                return resul.ToArray();
            }
            //================================================================================================ Всё для получения информации о фильме
            /// <summary>
            /// Получение данных трийлера и инфа о фильме или сериале
            /// </summary>
            private void GetTrillerAndInfo()
            {
                WebClient webClient = new WebClient();
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:76.0) Gecko/20100101 Firefox/76.0");
                webClient.Encoding = Encoding.UTF8;

                string Html;

                if (IsSerial)
                    Html = webClient.DownloadString("https://w84.zona.plus/tvseries/" + Name_id);
                else
                    Html = webClient.DownloadString("https://w84.zona.plus/movies/" + Name_id);

                GetTrillerHtml(Html);
                GetDetalInfoHtml(Html);
                ActivatedReqestTrillerAndInfo = true;
            }
            /// <summary>
            /// Если будутт данные по трийлеру то они быдут получены
            /// </summary>
            private void GetTrillerHtml(string Html)
            {
                if (Html.Contains("entity-default-btn trailer-btn js-trailer"))
                {
                    Html = Html.Remove(0, Html.IndexOf("entity-default-btn trailer-btn js-trailer"));
                    Html = Html.Remove(0, Html.IndexOf("data-link"));
                    Html = Html.Remove(0, Html.IndexOf('"') + 1);
                    Html = Html.Substring(0, Html.IndexOf('"'));
                    Triller = new string[] { Html };
                }
            }
            /// <summary>
            /// Получение детальной информации о фильме и сериале из Html страницы
            /// </summary>
            /// <param name="Html"></param>
            private void GetDetalInfoHtml(string Html)
            {
                // Жанр entity-desc-value js-genres
                genre = GetElemHtml(Html, "entity-desc-value js-genres");
                // Страна entity-desc-value js-countries
                country = GetElemHtml(Html, "entity-desc-value js-countries");
                // Режиссёр entity-desc-value is-narrow js-director
                producer = GetElemHtml(Html, "entity-desc-value is-narrow js-director", "name");
                // Сценарий entity-desc-value is-narrow js-scenarist
                scenario = GetElemHtml(Html, "entity-desc-value is-narrow js-scenarist", "name");
                // Актёры entity-desc-value is-narrow
                Actor = GetElemHtml(Html, "entity-desc-value is-narrow ", "name");
                // Инфо entity-desc-description
                if (Html.Contains("entity-desc-description"))
                {
                    info = Html.Remove(0, Html.IndexOf("entity-desc-description"));
                    info = info.Remove(0, info.IndexOf('>') + 1);
                    info = info.Substring(0, info.IndexOf('<'));
                }
                // Продолжительность 
                if (Html.Contains("\"entity-desc-value\""))
                {
                    string res = Html.Remove(0, Html.LastIndexOf("\"entity-desc-value\""));
                    res = res.Substring(0, res.IndexOf("</time>"));
                    res = res.Remove(0, res.IndexOf('>') + 1);
                    res = res.Remove(0, res.IndexOf('>') + 1);
                    res = res.Substring(0, res.IndexOf('<'));
                    time = res.Replace("\r", null).Replace("\n", null).Replace("  ", null);
                }
                // Премьера entity-desc-value is-narrow
                premiere = GetElemHtml(Html.Remove(0, Html.IndexOf("entity-desc-value is-narrow") + 5), "entity-desc-value is-narrow");
                if (premiere != null)
                {
                    List<string> resul = new List<string>(0);
                    foreach (var elem in premiere)
                        if (elem != null && elem != "")
                            resul.Add(elem.Replace("<br/>", null).Replace("&nbsp;", " "));
                    premiere = resul.ToArray();
                }
            }
            /// <summary>
            /// Получение информации из Html перечислений
            /// </summary>
            private static string[] GetElemHtml(string Html, string NameClass, string Key = null)
            {
                if (Html.Contains('"' + NameClass + '"'))
                {
                    List<string> values = new List<string>(0);
                    Html = Html.Remove(0, Html.IndexOf('"' + NameClass + '"'));
                    Html = Html.Substring(0, Html.IndexOf("</dd>"));
                    while (Html.Contains("</span>"))
                    {
                        string res = Html.Substring(0, Html.IndexOf("</span>"));
                        Html = Html.Remove(0, Html.IndexOf("</span>") + 7);
                        if ((Key != null ? res.Contains(Key) : true))
                        {
                            res = res.Remove(0, res.LastIndexOf('>') + 1);
                            values.Add(res);
                        }
                    }
                    return values.ToArray();
                }
                return null;
            }

            /// <summary>
            /// Подгрузка всей информации с сервера
            /// </summary>
            /// <param name="VideoGetMoreInfo">Подгружать ли видеочастям всю информацию?</param>
            public void GetMoreInfo(bool VideoGetMoreInfo = false)
            {
                  if (!ActivatedReqestVideos) GetVideos();
                  if (!ActivatedReqestTrillerAndInfo) GetTrillerAndInfo();
                  if (VideoGetMoreInfo) foreach (var season in Videos) foreach (var elem in season) elem.GetMoreInfo();
            }

            /// <summary>
            /// Данные видео
            /// </summary>
            public class DataVideo
            {
                /// <summary>
                /// id видеофайла
                /// </summary>
                public string Id { get; private set; }
                /// <summary>
                /// Состояние запрашивались ли фото
                /// </summary>
                private bool StateActivatedGetMoreFotosAndUrlVideo = false;

                private string urlVideo;
                /// <summary>
                /// Url видео
                /// </summary>
                public string UrlVideo
                {
                    get
                    {
                        if (StateActivatedGetMoreFotosAndUrlVideo)
                        {
                            return urlVideo;
                        }
                        else
                        {
                            GetData();
                            return urlVideo;
                        }
                    }
                    private set
                    {
                        urlVideo = value;
                    }
                }

                private string[] urlImg;
                /// <summary>
                /// Url фотографий видеофайла
                /// </summary>
                public string[] UrlImg
                {
                    get
                    {
                        if (StateActivatedGetMoreFotosAndUrlVideo)
                        {
                            return urlImg;
                        }
                        else
                        {
                            GetData();
                            return urlImg;
                        }
                    }
                    private set
                    {
                        urlImg = value;
                    }
                }

                public DataVideo(string id)
                {
                    this.Id = id;
                }
                /// <summary>
                /// Получение данных на видео и на фото
                /// </summary>
                private void GetData()
                {
                    byte Rest = 0;
                restart:
                    try
                    {
                        if (Id != null && Id != "" && Rest < 3)
                        {
                            string Html = (new WebClient()).DownloadString("https://w84.zona.plus/ajax/video/" + Id);
                            GetUrlVideoFilm(Html);
                            GetMoreFotosJson(Html);
                            StateActivatedGetMoreFotosAndUrlVideo = true;
                        }
                    }
                    catch
                    {
                        goto restart;
                    }
                }
                /// <summary>
                /// Получение из Json ссылки на фильм
                /// </summary>
                /// <param name="Json"></param>
                private void GetMoreFotosJson(string Json)
                {
                    if (Json.Contains("https"))
                    {
                        List<string> UrlsFotos = new List<string>(0);
                        Json = Json.Remove(0, Json.IndexOf("images"));
                        Json = Json.Remove(0, Json.IndexOf('[') + 1);
                        Json = Json.Substring(0, Json.IndexOf(']'));

                        string[] res = Json.Split(',');

                        foreach (var elem in res)
                        {
                            if (elem.Contains("https") && !UrlsFotos.Contains(elem.Replace("\\/", "/").Replace("\"", null)))
                            {
                                UrlsFotos.Add(elem.Replace("\\/", "/").Replace("\"", null));
                            }
                        }

                        UrlImg = UrlsFotos.ToArray();
                    }
                }
                /// <summary>
                /// Получение из Json ссылки на фильм
                /// </summary>
                /// <param name="Json"></param>
                private void GetUrlVideoFilm(string Json)
                {
                    if (Json.Contains("url"))
                    {
                        Json = Json.Remove(0, Json.IndexOf("url"));
                        Json = Json.Remove(0, Json.IndexOf(':'));
                        Json = Json.Remove(0, Json.IndexOf('"') + 1);
                        Json = Json.Substring(0, Json.IndexOf('"'));
                        UrlVideo = Json.Replace("\\/", "/");
                    }
                }

                /// <summary>
                /// Получение всей информации
                /// </summary>
                public void GetMoreInfo()
                {
                    if (!StateActivatedGetMoreFotosAndUrlVideo) GetData();
                }
            }
        }
        /// <summary>
        /// В ответе у них Json содержит ошибки и не получается десериализовать поэтому было принято решения написать парсер значений
        /// </summary>
        /// <param name="Json">Json txt</param>
        /// <param name="Name">Название искомой переменной</param>
        /// <returns>значение переменной</returns>
        private static string ParsJson(string Json, string Name)
        {

            string resul = null;
            try
            {
                resul = Json.Remove(0, Json.IndexOf('"' + Name + '"'));
                resul = resul.Remove(0, resul.IndexOf(":") + 1);
                int One = int.MaxValue, Two = int.MaxValue, Three = int.MaxValue;

                if (resul.Contains(',')) One = resul.IndexOf(',');
                if (resul.Contains(']')) Two = resul.IndexOf(']');
                if (resul.Contains('}')) Three = resul.IndexOf('}');

                resul = resul.Substring(0, Math.Min(Math.Min(One, Two), Three));

                if (resul.Contains("null")) return null;

                if (resul.Contains('\"'))
                {
                    resul = resul.Remove(0, resul.IndexOf('\"') + 1);
                    resul = resul.Substring(0, resul.IndexOf('\"'));
                    return resul;
                }
                // Эта часть проверки не подверглась
                return resul.Replace(" ", null).Replace("\r", null).Replace("\n", null).Replace("\t", null);
            }
            catch { }
            return resul;
        }
    }
    public class Filters
    {
        private static bool GetedFilters;

        private static string[][] genre;
        public static string[] Genre
        {
            get
            {
                List<string> res = new List<string>(0);
                foreach (var elem in genre)
                {
                    res.Add(elem[1]);
                }
                return res.ToArray();
            }
            private set { }
        }
        private static string[][] year;
        public static string[] Year
        {
            get
            {
                List<string> res = new List<string>(0);
                foreach (var elem in year)
                {
                    res.Add(elem[1]);
                }
                return res.ToArray();
            }
            private set { }
        }
        private static string[][] country;
        public static string[] Country
        {
            get
            {
                List<string> res = new List<string>(0);
                foreach (var elem in country)
                {
                    res.Add(elem[1]);
                }
                return res.ToArray();
            }
            private set { }
        }
        private static string[][] rating;
        public static string[] Rating
        {
            get
            {
                List<string> res = new List<string>(0);
                foreach (var elem in rating)
                {
                    res.Add(elem[1]);
                }
                return res.ToArray();
            }
            private set { }
        }
        private static string[][] sort;
        public static string[] Sort
        {
            get
            {
                List<string> res = new List<string>(0);
                foreach (var elem in sort)
                {
                    res.Add(elem[1]);
                }
                return res.ToArray();
            }
            private set { }
        }

        public string GenreSelected { get; private set; }
        public string YearSelected { get; private set; }
        public string CountrySelected { get; private set; }
        public string RatingSelected { get; private set; }
        public string SortSelected { get; private set; }
        public bool IsSerial { get; set; }

        public Filters()
        {
            if (!GetedFilters) GetFilters();
            SetGenreIndex(0);
            SetYearIndex(0);
            SetCountryIndex(0);
            SetRatingIndex(0);
            SetSortIndex(0);
        }
        /// <summary>
        /// Сбор данных для фильтра
        /// </summary>
        private static void GetFilters()
        {
            string Html = (new WebClient { Encoding = Encoding.UTF8 }).DownloadString("https://w84.zona.plus/movies");
            genre = GetFilters(Html, "filter-id-genreId");
            year = GetFilters(Html, "filter-id-year");
            country = GetFilters(Html, "filter-id-country_id");
            rating = GetFilters(Html, "filter-id-rating");
            sort = GetFilters(Html, "filter-id-sort");
            GetedFilters = true;
        }
        /// <summary>
        /// Получение выборки
        /// </summary>
        private static string[][] GetFilters(string Html, string ID)
        {
            List<string[]> elemsR = new List<string[]>(0);
            if (Html.Contains('"' + ID + '"'))
            {
                Html = Html.Remove(0, Html.IndexOf('"' + ID + '"'));
                Html = Html.Substring(0, Html.IndexOf("</select>"));
                while (Html.Contains("</option>"))
                {
                    string str = Html.Substring(0, Html.IndexOf("</option>") + 9);
                    Html = Html.Remove(0, Html.IndexOf("</option>") + 9);
                    elemsR.Add(new string[] { GetHtmlValue(str, "value"), GetHtmlValue(str, "label") });
                }
            }
            return elemsR.ToArray();
        }
        /// <summary>
        /// Получение значение по ключу
        /// </summary>
        private static string GetHtmlValue(string Html, string Key)
        {
            string res = null;
            if (Html.Contains(Key))
            {
                res = Html.Remove(0, Html.IndexOf(Key));
                res = res.Remove(0, res.IndexOf('"') + 1);
                return res.Substring(0, res.IndexOf('"'));
            }
            return res;
        }

        public void SetGenreNameRus(string NameRus)
        {
            foreach (var elem in genre)
            {
                if (elem[1] == NameRus)
                {
                    GenreSelected = elem[0];
                }
            }
        }
        public void SetGenreNameKey(string NameKey)
        {
            foreach (var elem in genre)
            {
                if (elem[0] == NameKey)
                {
                    GenreSelected = elem[0];
                }
            }
        }
        public void SetGenreIndex(uint index)
        {
            if (genre.Length > index)
            {
                GenreSelected = genre[index][0];
            }
        }

        public void SetYearNameRus(string NameRus)
        {
            foreach (var elem in year)
            {
                if (elem[1] == NameRus)
                {
                    YearSelected = elem[0];
                }
            }
        }
        public void SetYearNameKey(string NameKey)
        {
            foreach (var elem in year)
            {
                if (elem[0] == NameKey)
                {
                    YearSelected = elem[0];
                }
            }
        }
        public void SetYearIndex(uint index)
        {
            if (year.Length > index)
            {
                YearSelected = year[index][0];
            }
        }

        public void SetYearCountryRus(string NameRus)
        {
            foreach (var elem in country)
            {
                if (elem[1] == NameRus)
                {
                    CountrySelected = elem[0];
                }
            }
        }
        public void SetYearCountryKey(string NameKey)
        {
            foreach (var elem in country)
            {
                if (elem[0] == NameKey)
                {
                    CountrySelected = elem[0];
                }
            }
        }
        public void SetCountryIndex(uint index)
        {
            if (country.Length > index)
            {
                CountrySelected = country[index][0];
            }
        }

        public void SetYearRatingRus(string NameRus)
        {
            foreach (var elem in rating)
            {
                if (elem[1] == NameRus)
                {
                    RatingSelected = elem[0];
                }
            }
        }
        public void SetYearRatingKey(string NameKey)
        {
            foreach (var elem in rating)
            {
                if (elem[0] == NameKey)
                {
                    RatingSelected = elem[0];
                }
            }
        }
        public void SetRatingIndex(uint index)
        {
            if (rating.Length > index)
            {
                RatingSelected = rating[index][0];
            }
        }

        public void SetYearSortRus(string NameRus)
        {
            foreach (var elem in sort)
            {
                if (elem[1] == NameRus)
                {
                    SortSelected = elem[0];
                }
            }
        }
        public void SetYearSortKey(string NameKey)
        {
            foreach (var elem in sort)
            {
                if (elem[0] == NameKey)
                {
                    SortSelected = elem[0];
                }
            }
        }
        public void SetSortIndex(uint index)
        {
            if (sort.Length > index)
            {
                SortSelected = sort[index][0];
            }
        }
    }
}