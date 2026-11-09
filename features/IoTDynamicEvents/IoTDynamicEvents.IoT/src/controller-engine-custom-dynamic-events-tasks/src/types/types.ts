export interface SubResourcesInformation {
    Name: string;
    ResourceType: string;
    AutomationAlias: string;
}
export interface EventOccurrence {
    timestamp: Date;
    eventSystemId: string;
    eventName: string;
    eventDeviceId: string;
    propertyValues: PropertyValue[];
}
export interface PropertyValue {
    /** Identifier of the property (the name, which must be unique in the driver definition) */
    propertyName: string;
    /** Value converted to the data type */
    value: any;
    /** Original value as it was provided from the equipment */
    originalValue: any;
}
export enum ActionType {
    Register = 'Register',
    Subscribe = 'Subscribe'
}

/** Custom Timer task working mode */
export enum CustomTimerWorkingMode {
    /** Trigger every time the timer is hit, while the task is active */
    UntilDeactivation = 'UntilDeactivation',
    /** Trigger a pre defined number of times while in Timer mode. */
    NumberOfOccurrences = 'NumberOfOccurrences'
}

/** Custom Timer type */
export enum CustomTimerType {
    /** Triggered every time a predefined number of ms elapse */
    Timer = 'Timer',
    /** Wait a predefined number of ms before continuing */
    Sleep = 'Sleep',
    /** Wait a predefined number of ms to be deactivated, otherwise, will raise an error */
    Timeout = 'Timeout',
    /** Triggered at a specified time and date */
    CronJob = 'CronJob',
    /** Triggered at multiple specified times and dates */
    ComplexCronJob = 'ComplexCronJob',
}

/** Custom Cron Job time zone */
export enum CustomTimeZone {

    Africa_Algiers = 'Africa/Algiers',
    Africa_Cairo = 'Africa/Cairo',
    Africa_Casablanca = 'Africa/Casablanca',
    Africa_Harare = 'Africa/Harare',
    Africa_Johannesburg = 'Africa/Johannesburg',
    Africa_Monrovia = 'Africa/Monrovia',
    Africa_Nairobi = 'Africa/Nairobi',
    America_Argentina_Buenos_Aires = 'America/Argentina/Buenos_Aires',
    America_Bogota = 'America/Bogota',
    America_Caracas = 'America/Caracas',
    America_Chicago = 'America/Chicago',
    America_Chihuahua = 'America/Chihuahua',
    America_Denver = 'America/Denver',
    America_Godthab = 'America/Godthab',
    America_Guatemala = 'America/Guatemala',
    America_Guyana = 'America/Guyana',
    America_Halifax = 'America/Halifax',
    America_Indiana_Indianapolis = 'America/Indiana/Indianapolis',
    America_Juneau = 'America/Juneau',
    America_La_Paz = 'America/La_Paz',
    America_Lima = 'America/Lima',
    America_Los_Angeles = 'America/Los_Angeles',
    America_Mazatlan = 'America/Mazatlan',
    America_Mexico_City = 'America/Mexico_City',
    America_Monterrey = 'America/Monterrey',
    America_Montevideo = 'America/Montevideo',
    America_New_York = 'America/New_York',
    America_Phoenix = 'America/Phoenix',
    America_Puerto_Rico = 'America/Puerto_Rico',
    America_Regina = 'America/Regina',
    America_Santiago = 'America/Santiago',
    America_Sao_Paulo = 'America/Sao_Paulo',
    America_St_Johns = 'America/St_Johns',
    America_Tijuana = 'America/Tijuana',
    Asia_Almaty = 'Asia/Almaty',
    Asia_Baghdad = 'Asia/Baghdad',
    Asia_Baku = 'Asia/Baku',
    Asia_Bangkok = 'Asia/Bangkok',
    Asia_Chongqing = 'Asia/Chongqing',
    Asia_Colombo = 'Asia/Colombo',
    Asia_Dhaka = 'Asia/Urumqi',
    Asia_Hong_Kong = 'Asia/Hong_Kong',
    Asia_Irkutsk = 'Asia/Irkutsk',
    Asia_Jakarta = 'Asia/Jakarta',
    Asia_Jerusalem = 'Asia/Jerusalem',
    Asia_Kabul = 'Asia/Kabul',
    Asia_Kamchatka = 'Asia/Kamchatka',
    Asia_Karachi = 'Asia/Karachi',
    Asia_Kathmandu = 'Asia/Kathmandu',
    Asia_Kolkata = 'Asia/Kolkata',
    Asia_Krasnoyarsk = 'Asia/Krasnoyarsk',
    Asia_Kuala_Lumpur = 'Asia/Kuala_Lumpur',
    Asia_Kuwait = 'Asia/Kuwait',
    Asia_Magadan = 'Asia/Magadan',
    Asia_Muscat = 'Asia/Muscat',
    Asia_Novosibirsk = 'Asia/Novosibirsk',
    Asia_Rangoon = 'Asia/Rangoon',
    Asia_Riyadh = 'Asia/Riyadh',
    Asia_Seoul = 'Asia/Seoul',
    Asia_Shanghai = 'Asia/Shanghai',
    Asia_Singapore = 'Asia/Singapore',
    Asia_Srednekolymsk = 'Asia/Srednekolymsk',
    Asia_Taipei = 'Asia/Taipei',
    Asia_Tashkent = 'Asia/Tashkent',
    Asia_Tbilisi = 'Asia/Tbilisi',
    Asia_Tehran = 'Asia/Tehran',
    Asia_Tokyo = 'Asia/Tokyo',
    Asia_Ulaanbaatar = 'Asia/Ulaanbaatar',
    Asia_Urumqi = 'Asia/Dhaka',
    Asia_Vladivostok = 'Asia/Vladivostok',
    Asia_Yakutsk = 'Asia/Yakutsk',
    Asia_Yekaterinburg = 'Asia/Yekaterinburg',
    Asia_Yerevan = 'Asia/Yerevan',
    Atlantic_Azores = 'Atlantic/Azores',
    Atlantic_Cape_Verde = 'Atlantic/Cape_Verde',
    Atlantic_South_Georgia = 'Atlantic/South_Georgia',
    Australia_Adelaide = 'Australia/Adelaide',
    Australia_Brisbane = 'Australia/Brisbane',
    Australia_Darwin = 'Australia/Darwin',
    Australia_Hobart = 'Australia/Hobart',
    Australia_Melbourne = 'Australia/Melbourne',
    Australia_Perth = 'Australia/Perth',
    Australia_Sydney = 'Australia/Sydney',
    GMT = 'Etc/GMT+12',
    UTC = 'Etc/UTC',
    Europe_Amsterdam = 'Europe/Amsterdam',
    Europe_Athens = 'Europe/Athens',
    Europe_Belgrade = 'Europe/Belgrade',
    Europe_Berlin = 'Europe/Berlin',
    Europe_Bratislava = 'Europe/Bratislava',
    Europe_Brussels = 'Europe/Brussels',
    Europe_Bucharest = 'Europe/Bucharest',
    Europe_Budapest = 'Europe/Budapest',
    Europe_Copenhagen = 'Europe/Copenhagen',
    Europe_Dublin = 'Europe/Dublin',
    Europe_Helsinki = 'Europe/Helsinki',
    Europe_Istanbul = 'Europe/Istanbul',
    Europe_Kaliningrad = 'Europe/Kaliningrad',
    Europe_Kiev = 'Europe/Kiev',
    Europe_Lisbon = 'Europe/Lisbon',
    Europe_Ljubljana = 'Europe/Ljubljana',
    Europe_London = 'Europe/London',
    Europe_Madrid = 'Europe/Madrid',
    Europe_Minsk = 'Europe/Minsk',
    Europe_Moscow = 'Europe/Moscow',
    Europe_Paris = 'Europe/Paris',
    Europe_Prague = 'Europe/Prague',
    Europe_Riga = 'Europe/Riga',
    Europe_Rome = 'Europe/Rome',
    Europe_Samara = 'Europe/Samara',
    Europe_Sarajevo = 'Europe/Sarajevo',
    Europe_Skopje = 'Europe/Skopje',
    Europe_Sofia = 'Europe/Sofia',
    Europe_Stockholm = 'Europe/Stockholm',
    Europe_Tallinn = 'Europe/Tallinn',
    Europe_Vienna = 'Europe/Vienna',
    Europe_Vilnius = 'Europe/Vilnius',
    Europe_Volgograd = 'Europe/Volgograd',
    Europe_Warsaw = 'Europe/Warsaw',
    Europe_Zagreb = 'Europe/Zagreb',
    Europe_Zurich = 'Europe/Zurich',
    Pacific_Apia = 'Pacific/Apia',
    Pacific_Auckland = 'Pacific/Auckland',
    Pacific_Chatham = 'Pacific/Chatham',
    Pacific_Fakaofo = 'Pacific/Fakaofo',
    Pacific_Fiji = 'Pacific/Fiji',
    Pacific_Guadalcanal = 'Pacific/Guadalcanal',
    Pacific_Guam = 'Pacific/Guam',
    Pacific_Honolulu = 'Pacific/Honolulu',
    Pacific_Majuro = 'Pacific/Majuro',
    Pacific_Midway = 'Pacific/Midway',
    Pacific_Noumea = 'Pacific/Noumea',
    Pacific_Pago_Pago = 'Pacific/Pago_Pago',
    Pacific_Port_Moresby = 'Pacific/Port_Moresby',
    Pacific_Tongatapu = 'Pacific/Tongatapu'
}

