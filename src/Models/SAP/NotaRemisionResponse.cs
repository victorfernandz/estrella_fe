public class NotaRemisionResponse
{
    public DeliveryNotesData DeliveryNotes { get; set; }
    public DocumentLineData DocumentLines { get; set; }
    public BusinessPartnerData BusinessPartners { get; set; }
    public CurrenciesData Currencies { get; set; }
}

public class DeliveryNotesData
{
    public int DocEntry { get; set; }
    public string DocType { get; set; }
    public string U_FE_CDC { get; set; }
    public string U_FE_Estado { get; set; }
    public string U_FE_CODERR { get; set; }
    public string U_CENT_TIPO_DOC { get; set; }
    public string CardCode { get; set; }
    public string U_CENT_EST { get; set; }
    public string U_CENT_PE { get; set; }
    public string FolioNumber { get; set; }
    public string DocDate { get; set; }
    public int DocTime { get; set; }
    public string U_FITE { get; set; }
    public int U_CENT_TIMB { get; set; }
    public decimal DocRate { get; set; }
    // Referencia opcional a la Factura relacionada (mismo patrón que U_NUMFC/U_TIMFC en Notas de Crédito)
    public string U_NUMFC { get; set; }
    public int U_TIMFC { get; set; }
    // Código que enlaza con @EPY_NRDE (datos de la Nota de Remisión / transporte)
    public string U_NORE { get; set; }
    public string Comments { get; set; }
}

// @EPY_NRDE - Cabecera de datos de la Nota de Remisión (gCamNRE + gTransp)
public class EpyNrdeData
{
    public string Code { get; set; }
    public string Name { get; set; }

    // gCamNRE (E500-E506)
    public string U_MEMI { get; set; }   // Motivo de emisión (código SAP)
    public string U_REMI { get; set; }   // Responsable de la emisión (código SAP)
    public decimal? U_KMR { get; set; }  // Kilómetros estimados de recorrido
    public string U_FEMI { get; set; }   // Fecha futura de factura (no emitida aún)

    // gTransp (E900-E912)
    public string U_TITRA { get; set; }  // Tipo de transporte (código SAP)
    public string U_MOD { get; set; }    // Modalidad del transporte (código SAP)
    public string U_REFL { get; set; }   // Responsable del costo del flete (código SAP)
    public string U_CONE { get; set; }   // Condición de la negociación (Incoterms)
    public string U_INFO { get; set; }   // Número de manifiesto/BL/declaración de tránsito
    public string U_DESP { get; set; }   // Número de despacho de importación
    public string U_FINI { get; set; }   // Fecha estimada de inicio de traslado
    public string U_FFIN { get; set; }   // Fecha estimada de fin de traslado
    public string U_PAIS { get; set; }   // Código del país de destino

    // Enlaces a las tablas complementarias
    public string U_TRAN { get; set; }   // Código -> @EPY_TRAN (transportista)
    public string U_VEHI { get; set; }   // Código -> @EPY_VEHI (vehículo)
}

// @EPY_TRAN - Transportista / chofer / agente (gCamTrans, E980-E999)
public class EpyTranData
{
    public string Code { get; set; }
    public string U_CRDI { get; set; } // Naturaleza del transportista (código SAP)
    public string U_DIDE { get; set; } // Tipo de documento de identidad del transportista (código SAP)
    public string U_DIRA { get; set; } // Dirección del agente
    public string U_DIRC { get; set; } // Dirección del chofer
    public string U_DOMT { get; set; } // Domicilio fiscal del transportista
    public int? U_DRUC { get; set; }   // Dígito verificador del RUC del agente
    public int? U_DVEA { get; set; }   // Dígito verificador del RUC del transportista
    public string U_NACH { get; set; } // Nombre y apellido del chofer
    public string U_NACT { get; set; } // Nacionalidad del transportista
    public string U_NIDC { get; set; } // Número de documento de identidad del chofer
    public string U_NIDT { get; set; } // Número de documento de identidad del transportista
    public string U_NOAG { get; set; } // Nombre o razón social del agente
    public string U_NOMT { get; set; } // Nombre o razón social del transportista
    public string U_RUCA { get; set; } // RUC del agente
    public string U_RUCT { get; set; } // RUC del transportista
}

// @EPY_VEHI - Vehículo de traslado (gVehTras, E960-E979)
public class EpyVehiData
{
    public string Code { get; set; }
    public string U_DAVE { get; set; } // Datos adicionales del vehículo
    public string U_IDVE { get; set; } // Tipo de identificación del vehículo (código SAP)
    public string U_MARC { get; set; } // Marca
    public string U_NIDV { get; set; } // Número de identificación del vehículo
    public string U_NRMA { get; set; } // Número de matrícula del vehículo
    public string U_NVUE { get; set; } // Número de vuelo
    public string U_TIVE { get; set; } // Tipo de vehículo (código SAP)
}

// OWHS - Almacén, usado para resolver el local de salida (gCamSal, E920-E939)
public class WarehouseData
{
    public string WarehouseCode { get; set; }
    public string Street { get; set; }
    public string StreetNo { get; set; }
    // Mismo patrón que EPY_DEMPCollection (EmpresaService): código SIFEN ya resuelto a mano en SAP
    public string U_DEPT { get; set; }
    public string U_DIST { get; set; }
    public string U_BALO { get; set; }
}
