import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { FeatureFlags } from '../config/features';
import { logger } from '../services/logger';
import {
  buildReportingChain,
  getOrgEmployees,
  getOrgScope,
  setOrgScope,
  type OrgEmployee,
  type OrgScope,
} from '../services/org';
import {
  getNotificationPreferences,
  getSentNotifications,
  saveNotificationPreferences,
  sendNotification,
  type NotificationDraft,
  type NotificationItem,
  type NotificationPreference,
} from '../services/notifications';
import {
  getSelfServiceProfile,
  saveSelfServiceProfile,
  submitCertificateRequest,
  submitCbuUpdate,
  type SelfServiceProfile,
} from '../services/hr';
import { deleteFile, listFiles, uploadFile, type FileItem } from '../services/files';
import './Gestion.css';

type GestionProps = {
  features: FeatureFlags;
};

type PushPermission = NotificationPermission | 'unsupported';

const emptyProfile: SelfServiceProfile = {
  phone: '',
  address: '',
  emergencyName: '',
  emergencyPhone: '',
};

const emptyDraft: NotificationDraft = {
  title: '',
  message: '',
  audience: 'all',
  channels: {
    inApp: true,
    email: false,
    push: false,
    sms: false,
  },
};

export default function Gestion({ features }: GestionProps) {
  const navigate = useNavigate();

  const [orgEmployees, setOrgEmployees] = useState<OrgEmployee[]>([]);
  const [orgScopeState, setOrgScopeState] = useState<OrgScope>(getOrgScope());
  const [selectedEmployeeId, setSelectedEmployeeId] = useState('');
  const [orgLoading, setOrgLoading] = useState(true);
  const [orgError, setOrgError] = useState<string | null>(null);

  const [prefs, setPrefs] = useState<NotificationPreference | null>(null);
  const [prefsSaving, setPrefsSaving] = useState(false);
  const [notificationHistory, setNotificationHistory] = useState<NotificationItem[]>([]);
  const [notificationLoading, setNotificationLoading] = useState(true);
  const [notificationError, setNotificationError] = useState<string | null>(null);
  const [draft, setDraft] = useState<NotificationDraft>(emptyDraft);
  const [sendingNotification, setSendingNotification] = useState(false);

  const [profile, setProfile] = useState<SelfServiceProfile>(emptyProfile);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileMessage, setProfileMessage] = useState<string | null>(null);

  const [cbu, setCbu] = useState({ cbu: '', alias: '' });
  const [cbuSaving, setCbuSaving] = useState(false);
  const [cbuMessage, setCbuMessage] = useState<string | null>(null);

  const [certificate, setCertificate] = useState({
    type: 'laboral' as const,
    comment: '',
  });
  const [certificateSaving, setCertificateSaving] = useState(false);
  const [certificateMessage, setCertificateMessage] = useState<string | null>(null);

  const [files, setFiles] = useState<FileItem[]>([]);
  const [filesLoading, setFilesLoading] = useState(true);
  const [filesError, setFilesError] = useState<string | null>(null);
  const [fileToUpload, setFileToUpload] = useState<File | null>(null);
  const [fileDescription, setFileDescription] = useState('');
  const [fileUploading, setFileUploading] = useState(false);

  const [pushPermission, setPushPermission] = useState<PushPermission>(() => {
    if (typeof Notification === 'undefined') return 'unsupported';
    return Notification.permission;
  });

  useEffect(() => {
    let cancelled = false;
    const loadOrg = async () => {
      try {
        const data = await getOrgEmployees();
        if (cancelled) return;
        setOrgEmployees(data);
        setSelectedEmployeeId((prev) => prev || data[0]?.id || '');
      } catch (err) {
        logger.captureError(err, 'Gestion.loadOrg');
        if (!cancelled) setOrgError('No se pudo cargar el organigrama.');
      } finally {
        if (!cancelled) setOrgLoading(false);
      }
    };

    loadOrg();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    const loadNotifications = async () => {
      try {
        const [prefsResponse, historyResponse] = await Promise.all([
          getNotificationPreferences(),
          getSentNotifications(),
        ]);
        if (cancelled) return;
        setPrefs(prefsResponse);
        setNotificationHistory(historyResponse);
      } catch (err) {
        logger.captureError(err, 'Gestion.loadNotifications');
        if (!cancelled) setNotificationError('No se pudieron cargar las notificaciones.');
      } finally {
        if (!cancelled) setNotificationLoading(false);
      }
    };

    loadNotifications();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    const loadProfile = async () => {
      try {
        const data = await getSelfServiceProfile();
        if (!cancelled) setProfile(data);
      } catch (err) {
        logger.captureError(err, 'Gestion.loadProfile');
      }
    };

    loadProfile();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    const loadFiles = async () => {
      try {
        const data = await listFiles();
        if (!cancelled) setFiles(data);
      } catch (err) {
        logger.captureError(err, 'Gestion.loadFiles');
        if (!cancelled) setFilesError('No se pudieron cargar los archivos.');
      } finally {
        if (!cancelled) setFilesLoading(false);
      }
    };

    loadFiles();
    return () => {
      cancelled = true;
    };
  }, []);

  const reportingChain = useMemo(() => {
    if (!selectedEmployeeId) return [];
    return buildReportingChain(orgEmployees, selectedEmployeeId, orgScopeState);
  }, [orgEmployees, selectedEmployeeId, orgScopeState]);

  const selectedEmployee = useMemo(
    () => orgEmployees.find((emp) => emp.id === selectedEmployeeId),
    [orgEmployees, selectedEmployeeId]
  );

  const handleScopeChange = (nextScope: OrgScope) => {
    setOrgScope(nextScope);
    setOrgScopeState(nextScope);
  };

  const handleSavePrefs = async () => {
    if (!prefs) return;
    setPrefsSaving(true);
    try {
      await saveNotificationPreferences(prefs);
    } catch (err) {
      logger.captureError(err, 'Gestion.savePrefs');
    } finally {
      setPrefsSaving(false);
    }
  };

  const handleSendNotification = async () => {
    if (sendingNotification) return;
    if (!draft.title.trim() || !draft.message.trim()) return;
    const channels = draft.channels;
    const hasChannel = channels.inApp || channels.email || channels.push || channels.sms;
    if (!hasChannel) return;

    setSendingNotification(true);
    try {
      const sent = await sendNotification(draft);
      setNotificationHistory((prev) => [sent, ...prev]);
      setDraft((prev) => ({ ...prev, title: '', message: '' }));
    } catch (err) {
      logger.captureError(err, 'Gestion.sendNotification');
      setNotificationError('No se pudo enviar el comunicado.');
    } finally {
      setSendingNotification(false);
    }
  };

  const handleEnablePush = async () => {
    if (typeof Notification === 'undefined') {
      setPushPermission('unsupported');
      return;
    }
    const permission = await Notification.requestPermission();
    setPushPermission(permission);
    setPrefs((prev) => (prev ? { ...prev, push: permission === 'granted' } : prev));
  };

  const handleSaveProfile = async () => {
    setProfileSaving(true);
    setProfileMessage(null);
    try {
      await saveSelfServiceProfile(profile);
      setProfileMessage('Datos guardados.');
    } catch (err) {
      logger.captureError(err, 'Gestion.saveProfile');
      setProfileMessage('No se pudieron guardar los datos.');
    } finally {
      setProfileSaving(false);
    }
  };

  const handleSubmitCbu = async () => {
    if (!cbu.cbu.trim()) return;
    setCbuSaving(true);
    setCbuMessage(null);
    try {
      await submitCbuUpdate(cbu);
      setCbuMessage('Solicitud enviada.');
      setCbu({ cbu: '', alias: '' });
    } catch (err) {
      logger.captureError(err, 'Gestion.submitCbu');
      setCbuMessage('No se pudo enviar la solicitud.');
    } finally {
      setCbuSaving(false);
    }
  };

  const handleSubmitCertificate = async () => {
    if (!certificate.comment.trim()) return;
    setCertificateSaving(true);
    setCertificateMessage(null);
    try {
      await submitCertificateRequest(certificate);
      setCertificateMessage('Solicitud enviada.');
      setCertificate((prev) => ({ ...prev, comment: '' }));
    } catch (err) {
      logger.captureError(err, 'Gestion.submitCertificate');
      setCertificateMessage('No se pudo enviar la solicitud.');
    } finally {
      setCertificateSaving(false);
    }
  };

  const handleUploadFile = async () => {
    if (!fileToUpload || fileUploading) return;
    setFileUploading(true);
    setFilesError(null);
    try {
      const uploaded = await uploadFile(fileToUpload, fileDescription);
      setFiles((prev) => [uploaded, ...prev]);
      setFileToUpload(null);
      setFileDescription('');
    } catch (err) {
      logger.captureError(err, 'Gestion.uploadFile');
      setFilesError('No se pudo subir el archivo.');
    } finally {
      setFileUploading(false);
    }
  };

  const handleDeleteFile = async (fileId: string) => {
    try {
      await deleteFile(fileId);
      setFiles((prev) => prev.filter((item) => item.id !== fileId));
    } catch (err) {
      logger.captureError(err, 'Gestion.deleteFile');
      setFilesError('No se pudo eliminar el archivo.');
    }
  };

  const canSendNotification = draft.title.trim() && draft.message.trim()
    && (draft.channels.inApp || draft.channels.email || draft.channels.push || draft.channels.sms)
    && !sendingNotification;

  return (
    <div className="page-container">
      <nav className="navbar">
        <div className="nav-content">
          <h1>Portal Empleado</h1>
          <div className="nav-actions">
            <button onClick={() => navigate('/')} className="nav-link">
              Inicio
            </button>
            <button onClick={() => navigate('/recibo-sueldo')} className="nav-link">
              ReciboSueldo
            </button>
            {features.vacaciones && (
              <button onClick={() => navigate('/vacaciones')} className="nav-link">
                Vacaciones
              </button>
            )}
          </div>
        </div>
      </nav>

      <main className="main-content">
        <div className="page-card">
          <div className="gestion-header">
            <h2>Gestion de Personas</h2>
            <p>Organigrama, notificaciones y autogestion en un solo lugar.</p>
          </div>

          <div className="gestion-sections">
            <section className="section-card">
              <div className="section-header">
                <h3>Dependencias y visibilidad</h3>
                <span className="section-meta">
                  Alcance: {orgScopeState === 'school' ? 'Colegio (hasta Director)' : 'SAED completo'}
                </span>
              </div>

              {orgLoading && <p className="placeholder-text">Cargando organigrama...</p>}
              {orgError && <p className="placeholder-text">{orgError}</p>}

              {!orgLoading && !orgError && (
                <>
                  <div className="form-grid">
                    <label className="form-label">
                      Persona
                      <select
                        className="form-select"
                        value={selectedEmployeeId}
                        onChange={(event) => setSelectedEmployeeId(event.target.value)}
                      >
                        {orgEmployees.map((employee) => (
                          <option key={employee.id} value={employee.id}>
                            {employee.fullName} - {employee.role}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="form-label">
                      Visibilidad
                      <select
                        className="form-select"
                        value={orgScopeState}
                        onChange={(event) => handleScopeChange(event.target.value as OrgScope)}
                      >
                        <option value="school">Colegio (hasta Director)</option>
                        <option value="saed">SAED completo</option>
                      </select>
                    </label>
                  </div>

                  <div className="chain-card">
                    <div className="chain-title">Cadena de dependencia</div>
                    {reportingChain.length === 0 && (
                      <p className="placeholder-text">No hay datos para mostrar.</p>
                    )}
                    {reportingChain.length > 0 && (
                      <ul className="chain-list">
                        {reportingChain.map((employee, index) => (
                          <li key={employee.id} className="chain-item">
                            <span className="chain-position">{index + 1}</span>
                            <div className="chain-info">
                              <strong>{employee.fullName}</strong>
                              <span>{employee.role} · {employee.unit}</span>
                            </div>
                          </li>
                        ))}
                      </ul>
                    )}
                    {selectedEmployee && orgScopeState === 'school' && selectedEmployee.level !== 'director' && (
                      <div className="chain-note">
                        En colegios la visibilidad se limita al Director del colegio.
                      </div>
                    )}
                  </div>
                </>
              )}
            </section>

            <section className="section-card">
              <div className="section-header">
                <h3>Notificaciones y comunicados</h3>
                <span className="section-meta">In-app, push y email</span>
              </div>

              {notificationLoading && <p className="placeholder-text">Cargando notificaciones...</p>}
              {notificationError && <p className="placeholder-text">{notificationError}</p>}

              {!notificationLoading && (
                <>
                  <div className="notification-grid">
                    <div className="notification-box">
                      <h4>Preferencias del empleado</h4>
                      {prefs && (
                        <div className="toggle-list">
                          <label className="toggle-item">
                            <input
                              type="checkbox"
                              checked={prefs.inApp}
                              onChange={(event) => setPrefs((prev) => prev
                                ? { ...prev, inApp: event.target.checked }
                                : prev)}
                            />
                            In-app
                          </label>
                          <label className="toggle-item">
                            <input
                              type="checkbox"
                              checked={prefs.email}
                              onChange={(event) => setPrefs((prev) => prev
                                ? { ...prev, email: event.target.checked }
                                : prev)}
                            />
                            Email
                          </label>
                          <label className="toggle-item">
                            <input
                              type="checkbox"
                              checked={prefs.push}
                              onChange={(event) => setPrefs((prev) => prev
                                ? { ...prev, push: event.target.checked }
                                : prev)}
                            />
                            Push
                          </label>
                          <label className="toggle-item disabled">
                            <input type="checkbox" checked={prefs.sms} disabled />
                            Celular (proximamente)
                          </label>
                        </div>
                      )}

                      <div className="push-status">
                        Estado push: {pushPermission === 'unsupported'
                          ? 'No soportado'
                          : pushPermission === 'granted'
                            ? 'Habilitado'
                            : 'Pendiente'}
                      </div>

                      <button
                        className="primary-button"
                        onClick={handleEnablePush}
                        disabled={pushPermission === 'granted' || pushPermission === 'unsupported'}
                      >
                        Habilitar push
                      </button>

                      <button
                        className="secondary-button"
                        onClick={handleSavePrefs}
                        disabled={!prefs || prefsSaving}
                      >
                        {prefsSaving ? 'Guardando...' : 'Guardar preferencias'}
                      </button>
                    </div>

                    <div className="notification-box">
                      <h4>Nuevo comunicado</h4>
                      <label className="form-label">
                        Titulo
                        <input
                          className="form-input"
                          value={draft.title}
                          onChange={(event) => setDraft((prev) => ({ ...prev, title: event.target.value }))}
                        />
                      </label>
                      <label className="form-label">
                        Mensaje
                        <textarea
                          className="form-textarea"
                          value={draft.message}
                          onChange={(event) => setDraft((prev) => ({ ...prev, message: event.target.value }))}
                        />
                      </label>
                      <label className="form-label">
                        Audiencia
                        <select
                          className="form-select"
                          value={draft.audience}
                          onChange={(event) => setDraft((prev) => ({ ...prev, audience: event.target.value as NotificationDraft['audience'] }))}
                        >
                          <option value="all">Todos</option>
                          <option value="colegio">Colegio</option>
                          <option value="area">Area / Sede</option>
                        </select>
                      </label>
                      <div className="toggle-list">
                        <label className="toggle-item">
                          <input
                            type="checkbox"
                            checked={draft.channels.inApp}
                            onChange={(event) => setDraft((prev) => ({
                              ...prev,
                              channels: { ...prev.channels, inApp: event.target.checked },
                            }))}
                          />
                          In-app
                        </label>
                        <label className="toggle-item">
                          <input
                            type="checkbox"
                            checked={draft.channels.email}
                            onChange={(event) => setDraft((prev) => ({
                              ...prev,
                              channels: { ...prev.channels, email: event.target.checked },
                            }))}
                          />
                          Email
                        </label>
                        <label className="toggle-item">
                          <input
                            type="checkbox"
                            checked={draft.channels.push}
                            onChange={(event) => setDraft((prev) => ({
                              ...prev,
                              channels: { ...prev.channels, push: event.target.checked },
                            }))}
                          />
                          Push
                        </label>
                      </div>
                      <button
                        className="primary-button"
                        onClick={handleSendNotification}
                        disabled={!canSendNotification}
                      >
                        {sendingNotification ? 'Enviando...' : 'Enviar comunicado'}
                      </button>
                    </div>
                  </div>

                  <div className="notification-history">
                    <h4>Historial reciente</h4>
                    {notificationHistory.length === 0 && (
                      <p className="placeholder-text">No hay comunicados enviados.</p>
                    )}
                    {notificationHistory.length > 0 && (
                      <ul className="history-list">
                        {notificationHistory.map((item) => (
                          <li key={item.id} className="history-item">
                            <div>
                              <strong>{item.title}</strong>
                              <p>{item.message}</p>
                              <span className="history-meta">
                                {new Date(item.createdAt).toLocaleString('es-AR')}
                                {' · '}
                                {item.audience}
                              </span>
                            </div>
                            <div className="history-tags">
                              {item.channels.inApp && <span className="tag">In-app</span>}
                              {item.channels.email && <span className="tag">Email</span>}
                              {item.channels.push && <span className="tag">Push</span>}
                              {item.channels.sms && <span className="tag muted">SMS</span>}
                            </div>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                </>
              )}
            </section>

            <section className="section-card">
              <div className="section-header">
                <h3>Autogestion del empleado</h3>
                <span className="section-meta">Datos personales y solicitudes</span>
              </div>

              <div className="self-service-grid">
                <div className="self-service-box">
                  <h4>Datos personales</h4>
                  <label className="form-label">
                    Telefono
                    <input
                      className="form-input"
                      value={profile.phone}
                      onChange={(event) => setProfile((prev) => ({ ...prev, phone: event.target.value }))}
                    />
                  </label>
                  <label className="form-label">
                    Direccion
                    <input
                      className="form-input"
                      value={profile.address}
                      onChange={(event) => setProfile((prev) => ({ ...prev, address: event.target.value }))}
                    />
                  </label>
                  <label className="form-label">
                    Contacto de emergencia
                    <input
                      className="form-input"
                      value={profile.emergencyName}
                      onChange={(event) => setProfile((prev) => ({ ...prev, emergencyName: event.target.value }))}
                    />
                  </label>
                  <label className="form-label">
                    Telefono emergencia
                    <input
                      className="form-input"
                      value={profile.emergencyPhone}
                      onChange={(event) => setProfile((prev) => ({ ...prev, emergencyPhone: event.target.value }))}
                    />
                  </label>
                  {profileMessage && <p className="status-text">{profileMessage}</p>}
                  <button
                    className="primary-button"
                    onClick={handleSaveProfile}
                    disabled={profileSaving}
                  >
                    {profileSaving ? 'Guardando...' : 'Guardar datos'}
                  </button>
                </div>

                <div className="self-service-box">
                  <h4>Cambio de CBU</h4>
                  <label className="form-label">
                    CBU
                    <input
                      className="form-input"
                      value={cbu.cbu}
                      onChange={(event) => setCbu((prev) => ({ ...prev, cbu: event.target.value }))}
                    />
                  </label>
                  <label className="form-label">
                    Alias (opcional)
                    <input
                      className="form-input"
                      value={cbu.alias}
                      onChange={(event) => setCbu((prev) => ({ ...prev, alias: event.target.value }))}
                    />
                  </label>
                  {cbuMessage && <p className="status-text">{cbuMessage}</p>}
                  <button
                    className="primary-button"
                    onClick={handleSubmitCbu}
                    disabled={cbuSaving || !cbu.cbu.trim()}
                  >
                    {cbuSaving ? 'Enviando...' : 'Enviar solicitud'}
                  </button>
                </div>

                <div className="self-service-box">
                  <h4>Solicitud de certificados</h4>
                  <label className="form-label">
                    Tipo
                    <select
                      className="form-select"
                      value={certificate.type}
                      onChange={(event) => setCertificate((prev) => ({
                        ...prev,
                        type: event.target.value as typeof certificate.type,
                      }))}
                    >
                      <option value="laboral">Constancia laboral</option>
                      <option value="sueldo">Constancia de sueldo</option>
                      <option value="antiguedad">Constancia de antiguedad</option>
                    </select>
                  </label>
                  <label className="form-label">
                    Comentario
                    <textarea
                      className="form-textarea"
                      value={certificate.comment}
                      onChange={(event) => setCertificate((prev) => ({ ...prev, comment: event.target.value }))}
                    />
                  </label>
                  {certificateMessage && <p className="status-text">{certificateMessage}</p>}
                  <button
                    className="primary-button"
                    onClick={handleSubmitCertificate}
                    disabled={certificateSaving || !certificate.comment.trim()}
                  >
                    {certificateSaving ? 'Enviando...' : 'Enviar solicitud'}
                  </button>
                </div>

                <div className="self-service-box">
                  <h4>Vacaciones</h4>
                  <p className="placeholder-text">
                    Acceso directo a la solicitud de vacaciones.
                  </p>
                  <button className="secondary-button" onClick={() => navigate('/vacaciones')}>
                    Ir a vacaciones
                  </button>
                </div>
              </div>
            </section>

            <section className="section-card">
              <div className="section-header">
                <h3>Archivos en S3</h3>
                <span className="section-meta">Carga y administracion de documentos</span>
              </div>

              <div className="files-grid">
                <div className="files-upload">
                  <label className="form-label">
                    Archivo
                    <input
                      className="form-input"
                      type="file"
                      onChange={(event) => setFileToUpload(event.target.files?.[0] ?? null)}
                    />
                  </label>
                  <label className="form-label">
                    Descripcion
                    <input
                      className="form-input"
                      value={fileDescription}
                      onChange={(event) => setFileDescription(event.target.value)}
                    />
                  </label>
                  <button
                    className="primary-button"
                    onClick={handleUploadFile}
                    disabled={!fileToUpload || fileUploading}
                  >
                    {fileUploading ? 'Subiendo...' : 'Subir archivo'}
                  </button>
                  <p className="hint-text">
                    Si no hay backend configurado, el archivo queda en estado pendiente.
                  </p>
                </div>

                <div className="files-list">
                  <h4>Documentos cargados</h4>
                  {filesLoading && <p className="placeholder-text">Cargando archivos...</p>}
                  {filesError && <p className="placeholder-text">{filesError}</p>}
                  {!filesLoading && files.length === 0 && (
                    <p className="placeholder-text">Todavia no hay archivos.</p>
                  )}
                  {files.length > 0 && (
                    <ul className="files-items">
                      {files.map((file) => (
                        <li key={file.id} className="file-item">
                          <div>
                            <strong>{file.name}</strong>
                            <span className="file-meta">
                              {formatFileSize(file.size)} · {file.status === 'uploaded' ? 'Subido' : 'Pendiente'}
                            </span>
                            {file.description && <span className="file-meta">{file.description}</span>}
                          </div>
                          <div className="file-actions">
                            {file.url ? (
                              <a className="link-button" href={file.url} target="_blank" rel="noreferrer">
                                Descargar
                              </a>
                            ) : (
                              <span className="link-button disabled">Sin enlace</span>
                            )}
                            <button
                              className="danger-button"
                              onClick={() => handleDeleteFile(file.id)}
                            >
                              Eliminar
                            </button>
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            </section>
          </div>
        </div>
      </main>
    </div>
  );
}

function formatFileSize(size: number): string {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
  if (size < 1024 * 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
  return `${(size / 1024 / 1024 / 1024).toFixed(1)} GB`;
}
