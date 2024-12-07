import React, {useEffect, useRef, useState} from "react";
import {useNavigate} from "react-router-dom";
import { Menu } from 'primereact/menu';
import { Sidebar } from 'primereact/sidebar';
import { Button } from 'primereact/button';
import {Toast} from "primereact/toast";
import {Avatar} from "primereact/avatar";
import {ScrollTop} from "primereact/scrolltop";
import {ConfirmDialog, confirmDialog} from 'primereact/confirmdialog';
import { Toolbar } from 'primereact/toolbar';
import {isNullOrUndef} from "chart.js/helpers";
import {Menubar} from "primereact/menubar";
import {Dialog} from "primereact/dialog";
import {FloatLabel} from "primereact/floatlabel";
import {InputText} from "primereact/inputtext";
import {FileUpload} from "primereact/fileupload";
import {Editor} from "primereact/editor";


// выход из аккаунта пользователя
export function LogoutUser(){
    const credentialsJSON = localStorage.getItem('userCredentials');
    const data = credentialsJSON ? JSON.parse(credentialsJSON) : [];
    const index = parseInt(localStorage.getItem('curUser'));

    // удаляем аккаунт из списка
    data.splice(index, 1);

    if(data.length === 0) {
        localStorage.removeItem('userCredentials');
        localStorage.removeItem('curUser');
    }
    else {
        localStorage.setItem('userCredentials', JSON.stringify(data));
        localStorage.setItem('curUser', '0');
    }

    window.location.reload();
}

// собственно сборка верхней панели
export default function ShowUpperBar() {
    // для уведомлений Toast
    const toast = useRef(null);

    // используется для redirect
    const navigate = useNavigate();

    // отображение выезжающей панели (SlideBar)
    const [visible, setVisible] = useState(false);

    // Действия: смена аккаунта и добавление аккаунта
    const [accountsOption, setAccountsOption] = useState([]);

    // текущий пользователь
    const [curAccount, setCurAccount] = useState(null);

    // диалоговое окно для отправки письма
    const [visibleLetterDialog, setVisibleLetterDialog] = useState(false);

    // диалоговое окно для отправки запроса дружбы (обмен ключами)
    const [visibleFriendDialog, setVisibleFriendDialog] = useState(false);

    // получатель письма
    const [mailTo, setMailTo] = useState('');

    // тема письма
    const [subject, setSubject] = useState('');

    // текст письма
    const [mailBody, setMailBody] = useState('');

    // адрес на который будет отправлен запрос дружбы
    const [friend, setFriend] = useState('');

    // отключение кнопки "Отправить письмо"
    const [disableSendLetter, setDisableSendLetter] = useState(false);

    // отключение кнопки "Отправить запрос дружбы"
    const [disableSendFriend, setDisableSendFriend] = useState(false);

    // Для хранения выбранных файлов
    const [files, setFiles] = useState([]);

    // Обработчик выбора файлов
    const onFileSelect = (event) => {
        setFiles(event.files); // Обновляем список выбранных файлов
    };

    // если пользователь подтвердил выход из аккаунта
    const accept = () => LogoutUser();

    // если нет подтверждения, то ничего не делаем
    const reject = () => {}

    const userAcc = () => {
        if(isNullOrUndef(localStorage.getItem('curUser')))
            navigate('/login')
        else
            setVisible(true);
    };

    useEffect(() => {
        if(!isNullOrUndef(localStorage.getItem('userCredentials'))){
            const credentialsJSON = localStorage.getItem('userCredentials');

            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            setCurAccount(data[index]?.email);
        }
    }, []);

    const setNewUser = (newEmail) => {
        const credentialsJSON = localStorage.getItem('userCredentials');

        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        let index = data.findIndex(account => account.email === newEmail);

        localStorage.setItem('curUser', `${index}`);

        window.location.reload();
    }

    useEffect(() => {
        if(!isNullOrUndef(localStorage.getItem('userCredentials')) && curAccount !== null) {
            const credentialsJSON = localStorage.getItem('userCredentials');
            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            // не выводим текущий аккаунт ещё и снизу
            let options = data.filter(obj => obj.email !== curAccount)
                .map((account) => {
                    return {
                        label: account.email,
                        command: () => {
                            setNewUser(account.email);
                        }
                    };
                });

            options.push({
                label: 'Добавить аккаунт',
                icon: 'pi pi-plus-circle',
                command: () => {
                    setVisible(false);
                    navigate('/login');
                }
            });

            setAccountsOption(options);
        }
    }, [curAccount]);

    let items = [
        {
            label: 'Текущий аккаунт',
            items: [
                {
                    label: curAccount,
                    icon: 'pi pi-user',
                    disabled: true
                },
                {
                    label: 'Выйти',
                    icon: 'pi pi-sign-out',
                    command: () => confirmDialog({
                        message: 'Вы уже вошли в аккаунт. Хотите выйти?',
                        header: 'Подтверждение',
                        icon: 'pi pi-exclamation-triangle',
                        defaultFocus: 'accept',
                        rejectLabel: 'Нет',
                        acceptIcon: 'pi pi-sign-out',
                        acceptLabel: 'Да',
                        accept,
                        reject
                    })
                }
            ]
        },
        {
            label: 'Аккаунты',
            items: [...accountsOption]
        }
    ];

    const endContent = (
        <React.Fragment>
            <Avatar icon="pi pi-user"
                    size="large"
                    className="user-profile"
                    shape="circle"
                    onClick={userAcc}/>
            <ConfirmDialog/>
            <Sidebar visible={visible}
                     position="right"
                     onHide={() => setVisible(false)}>
                <Menu model={items} style={{width: '100%'}}/>
            </Sidebar>
        </React.Fragment>
    );

    const actionItems = [
        {
            label: 'Написать письмо',
            icon: 'pi pi-envelope',
            disabled: disableSendLetter,
            command: () => {
                setVisibleLetterDialog(true);
            }
        },
        {
            label: 'Отправить запрос дружбы',
            icon: 'pi pi-user-plus',
            disabled: disableSendFriend,
            command: () => {
                setVisibleFriendDialog(true);
            }
        }
    ];

    // футер для диалогового окна отправки запроса дружбы
    const footerFriendContent = (
        <div>
            <Button label="Отправить"
                    icon="pi pi-check"
                    onClick={() => {
                        const credentialsJSON = localStorage.getItem('userCredentials');

                        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

                        const index = parseInt(localStorage.getItem('curUser'));

                        if (friend !== '') {
                            setVisibleFriendDialog(false);

                            setDisableSendFriend(true);

                            sendFriendRequest(data[index]?.email);
                        }
                        else {
                            toast.current.show({
                                severity: 'error',
                                summary: 'Ошибка',
                                detail: "Введите получателя запроса"
                            });
                        }
                    }}
            />
        </div>
    );

    // попытка отправки запроса дружбы
    async function sendFriendRequest(from) {
        try {
            const response = await fetch("http://localhost:5113/friends/SendFriendRequest", {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    EmailFrom: from,
                    EmailTo: friend
                }),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }

            const data = await response.text();
            toast.current.show({
                severity: 'success',
                summary: 'Запрос отправлен',
                detail: "Запрос дружбы отправлен пользователю"
            });

            // сброс данных после отправки
            setFriend('');
            setDisableSendFriend(false);
        } catch (error) {
            setDisableSendFriend(false);
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    // футер диалогового окна отправки письма
    const footerLetterContent = (
        <div>
            <Button label="Отправить"
                    icon="pi pi-check"
                    onClick={() => {
                        const credentialsJSON = localStorage.getItem('userCredentials');

                        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

                        const index = parseInt(localStorage.getItem('curUser'));

                        if (mailTo !== ''){
                            setVisibleLetterDialog(false);

                            setDisableSendLetter(true);

                            const formData = new FormData();
                            formData.append("Email", data[index]?.email);
                            formData.append("AppPassword", data[index]?.password);
                            formData.append("RecipientEmail", mailTo);
                            formData.append("Subject", subject);

                            mailBody !== '' && formData.append("Body", mailBody);

                            // Добавляем файлы
                            files.forEach((file) => {
                                formData.append("Files", file);
                            });

                            fetch("http://localhost:5113/mail/SendMail", {
                                method: "POST",
                                body: formData
                            })
                                .then(response => {
                                    // Обработка ответа от сервера
                                    if (!response.ok) {
                                        // Проверка на ошибки HTTP (4xx или 5xx)
                                        // Создаём ошибку, если ответ не успешный.
                                        throw new Error(`HTTP error! status: ${response.status}`);
                                    }
                                    return response;
                                })
                                .then(data => {
                                    toast.current.show({
                                        severity: 'success',
                                        summary: 'Письмо отправлено',
                                        detail: `Письмо отправлено пользователю ${mailTo}`
                                    });

                                    // сброс данных после отправки
                                    setMailTo('');
                                    setSubject('');
                                    setMailBody('');
                                    setFiles([]);
                                    setDisableSendLetter(false);
                                })
                                .catch(error => {
                                    setDisableSendLetter(false);
                                    // Обработка ошибок
                                    console.error("Ошибка:", error);
                                    toast.current.show({
                                        severity: 'error',
                                        summary: 'Ошибка',
                                        detail: 'Что-то пошло не так :('
                                    });
                                });
                        }
                        else {
                            toast.current.show({
                                severity: 'error',
                                summary: 'Ошибка',
                                detail: 'Введите почту получателя'
                            });
                        }
                    }}
            />
        </div>
    );

    const startContent = () => {
        if(!isNullOrUndef(localStorage.getItem('curUser'))){
            return(
                <React.Fragment>
                    <Menubar model={actionItems}
                             style={{width: '100%'}}
                    />
                    <Dialog header="Написать письмо"
                            visible={visibleLetterDialog}
                            style={{ width: '60vw' }}
                            onHide={() => {if (!visibleLetterDialog) return; setVisibleLetterDialog(false); }}
                            footer={footerLetterContent}
                    >
                        <div className="card">
                            <div className="flex justify-content-around"
                                 style={{width: '100%', padding: '20px'}}
                            >
                                <FloatLabel style={{ margin: '5px', width: '50%'}}>
                                    <InputText id="mailTo"
                                               value={mailTo}
                                               style={{width: '100%'}}
                                               onChange={(e) => setMailTo(e.target.value)}
                                    />
                                    <label htmlFor="mailTo">Кому</label>
                                </FloatLabel>

                                <FloatLabel style={{ margin: '5px' , width: '50%'}}>
                                    <InputText id="subject"
                                               value={subject}
                                               style={{width: '100%'}}
                                               onChange={(e) => setSubject(e.target.value)}
                                    />
                                    <label htmlFor="subject">Тема</label>
                                </FloatLabel>
                            </div>

                            <div className="m-2">
                                <FileUpload name="files[]"
                                            multiple
                                            customUpload
                                            uploadHandler={onFileSelect}
                                            onSelect={onFileSelect}
                                            chooseLabel="Выбрать файлы"
                                            uploadLabel="Загрузить"
                                            cancelLabel="Сбросить"
                                />
                            </div>

                            <Editor value={mailBody}
                                    onTextChange={(e) => setMailBody(e.htmlValue)}
                                    style={{ height: '320px' }} />

                        </div>
                    </Dialog>


                    <Dialog header="Отправить запрос дружбы"
                            visible={visibleFriendDialog}
                            style={{ width: '30vw' }}
                            onHide={() => {if (!visibleFriendDialog) return; setVisibleFriendDialog(false); }}
                            footer={footerFriendContent}
                    >
                        <div className="card flex justify-content-around"
                             style={{width: '100%', padding: '20px'}}>
                            <FloatLabel style={{ margin: '5px', width: '100%'}}>
                                <InputText id="friend"
                                           value={friend}
                                           style={{width: '100%'}}
                                           onChange={(e) => setFriend(e.target.value)}
                                />
                                <label htmlFor="mailTo">Кому</label>
                            </FloatLabel>
                        </div>
                    </Dialog>
                    <Toast ref={toast}/>
                </React.Fragment>);
        }
        else
            return (<></>);
    };

    return (
        <div className="App">
            <Toolbar start={startContent} end={endContent} />
            <ScrollTop/>
        </div>
    );
}
